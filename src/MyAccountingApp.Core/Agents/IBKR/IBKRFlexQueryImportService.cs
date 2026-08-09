namespace MyAccountingApp.Core.Agents.IBKR;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class IBKRFlexQueryImportService : IBrokerImportService
{
    private readonly List<IIBKRStatementAgent> agents;

    public IBKRFlexQueryImportService(IEnumerable<IIBKRStatementAgent> agents)
    {
        this.agents = agents.ToList();
    }

    public async Task<(IEnumerable<Transaction> Transactions, IEnumerable<AssetTransaction> AssetTransactions, IEnumerable<OptionTransaction> OptionTransactions)> ParseAllAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        Dictionary<string, List<string[]>> sections = GroupLinesBySection(lines);

        List<Transaction> transactions = new();
        List<AssetTransaction> assetTransactions = new();
        List<OptionTransaction> optionTransactions = new();
        List<string> errors = new();

        foreach (IIBKRStatementAgent agent in this.agents)
        {
            if (sections.TryGetValue(agent.SectionName, out List<string[]>? rows))
            {
                agent.Parse(rows, transactions, assetTransactions, optionTransactions, errors);
            }
        }

        return (transactions, assetTransactions, optionTransactions);
    }

    public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<AssetTransaction>());
    }

    private static Dictionary<string, List<string[]>> GroupLinesBySection(string[] lines)
    {
        Dictionary<string, List<string[]>> sections = new();
        string? currentSection = null;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] fields = SplitCsvLine(line);
            if (fields.Length == 0)
            {
                continue;
            }

            string section = fields[0];
            if (fields.Length >= 2 && fields[1] == "Header")
            {
                currentSection = section;
                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = new List<string[]>();
                }

                continue;
            }

            if (fields.Length >= 2 && fields[1] == "Data")
            {
                if (currentSection != null)
                {
                    if (!sections.ContainsKey(currentSection))
                    {
                        sections[currentSection] = new List<string[]>();
                    }

                    sections[currentSection].Add(fields);
                }
            }
        }

        return sections;
    }

    private static string[] SplitCsvLine(string line)
    {
        List<string> fields = new();
        bool inQuotes = false;
        StringBuilder current = new();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());
        return fields.ToArray();
    }
}
