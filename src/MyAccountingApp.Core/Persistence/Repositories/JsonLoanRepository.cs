namespace MyAccountingApp.Core.Persistence;

using System.Text.Json;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class JsonLoanRepository : ILoanRepository
{
    private readonly string filePath;
    private readonly IVaultService? _vaultService;

    public JsonLoanRepository(string filePath, IVaultService? vaultService = null)
    {
        this.filePath = filePath;
        this._vaultService = vaultService;
    }

    public IEnumerable<Loan> GetAll()
    {
        string json = EncryptedJsonFileStorage.ReadText(this.filePath, this._vaultService);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Loan>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Loan>>(json) ?? new List<Loan>();
        }
        catch
        {
            return new List<Loan>();
        }
    }

    public Loan? GetById(Guid id) => this.GetAll().FirstOrDefault(l => l.Id == id);

    public void Add(Loan loan)
    {
        List<Loan> loans = this.GetAll().ToList();
        loans.Add(loan);
        this.WriteAll(loans);
    }

    public void Update(Loan loan)
    {
        List<Loan> loans = this.GetAll().ToList();
        int index = loans.FindIndex(l => l.Id == loan.Id);
        if (index >= 0)
        {
            loans[index] = loan;
            this.WriteAll(loans);
        }
    }

    public bool Delete(Guid id)
    {
        List<Loan> loans = this.GetAll().ToList();
        int removed = loans.RemoveAll(l => l.Id == id);
        if (removed > 0)
        {
            this.WriteAll(loans);
        }

        return removed > 0;
    }

    public void Initialize(IEnumerable<Loan> loans)
    {
        this.WriteAll(loans.ToList());
    }

    private void WriteAll(List<Loan> loans)
    {
        string? dir = Path.GetDirectoryName(this.filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(loans, new JsonSerializerOptions { WriteIndented = true });
        EncryptedJsonFileStorage.WriteText(this.filePath, json, this._vaultService);
    }
}