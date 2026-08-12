namespace MyAccountingApp.Core.Persistence;
using System.Text.Json;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class JsonOptionTransactionRepository : IOptionTransactionRepository
{
    private readonly string filePath;
    private readonly IVaultService? _vaultService;

    public JsonOptionTransactionRepository(string filePath, IVaultService? vaultService = null)
    {
        this.filePath = filePath;
        this._vaultService = vaultService;
    }

    public IEnumerable<OptionTransaction> GetAll()
    {
        string json = EncryptedJsonFileStorage.ReadText(this.filePath, this._vaultService);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<OptionTransaction>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<OptionTransaction>>(json) ?? new List<OptionTransaction>();
        }
        catch
        {
            return new List<OptionTransaction>();
        }
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
        int index = transactions.FindIndex(t => t.Transaction.Id == transaction.Transaction.Id);
        if (index >= 0)
        {
            transactions[index] = transaction;
            this.WriteAll(transactions);
        }
    }

    public bool Delete(Guid id)
    {
        List<OptionTransaction> transactions = this.GetAll().ToList();
        int removed = transactions.RemoveAll(t => t.Transaction.Id == id);
        if (removed > 0)
        {
            this.WriteAll(transactions);
        }

        return removed > 0;
    }

    public int DeleteByYear(int year)
    {
        List<OptionTransaction> transactions = this.GetAll().ToList();
        int removed = transactions.RemoveAll(t => t.Transaction.Date.Year == year);
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
        EncryptedJsonFileStorage.WriteText(this.filePath, json, this._vaultService);
    }
}
