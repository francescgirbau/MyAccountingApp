namespace MyAccountingApp.Core.Repositories;

using System.Text.Json;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class JsonOptionTransactionRepository : IOptionTransactionRepository
{
    private readonly string filePath;

    public JsonOptionTransactionRepository(string filePath)
    {
        this.filePath = filePath;
    }

    public IEnumerable<OptionTransaction> GetAll()
    {
        if (!File.Exists(this.filePath) || new FileInfo(this.filePath).Length == 0)
        {
            return new List<OptionTransaction>();
        }

        string json = File.ReadAllText(this.filePath);
        return JsonSerializer.Deserialize<List<OptionTransaction>>(json) ?? new List<OptionTransaction>();
    }

    public void Add(OptionTransaction transaction)
    {
        List<OptionTransaction> transactions = this.GetAll().ToList();
        transactions.Add(transaction);
        this.WriteAll(transactions);
    }

    public void Update(OptionTransaction transaction)
    {
        List<OptionTransaction> transactions = this.GetAll().ToList();
        int index = transactions.FindIndex(t => t.Id == transaction.Id);
        if (index >= 0)
        {
            transactions[index] = transaction;
            this.WriteAll(transactions);
        }
    }

    public bool Delete(Guid id)
    {
        List<OptionTransaction> transactions = this.GetAll().ToList();
        int removed = transactions.RemoveAll(t => t.Id == id);
        if (removed > 0)
        {
            this.WriteAll(transactions);
        }

        return removed > 0;
    }

    public int DeleteByYear(int year)
    {
        List<OptionTransaction> transactions = this.GetAll().ToList();
        int removed = transactions.RemoveAll(t => t.Date.Year == year);
        if (removed > 0)
        {
            this.WriteAll(transactions);
        }

        return removed;
    }

    public void Initialize(IEnumerable<OptionTransaction> transactions)
    {
        this.WriteAll(transactions.ToList());
    }

    private void WriteAll(List<OptionTransaction> transactions)
    {
        string? dir = Path.GetDirectoryName(this.filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(transactions, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(this.filePath, json);
    }
}
