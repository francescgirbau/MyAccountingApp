namespace MyAccountingApp.Core.Persistence;

using System.Text.Json;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Interfaces;

public class JsonLoanMovementRepository : ILoanMovementRepository
{
    private readonly string filePath;
    private readonly IVaultService? _vaultService;

    public JsonLoanMovementRepository(string filePath, IVaultService? vaultService = null)
    {
        this.filePath = filePath;
        this._vaultService = vaultService;
    }

    public IEnumerable<LoanMovement> GetAll()
    {
        string json = EncryptedJsonFileStorage.ReadText(this.filePath, this._vaultService);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<LoanMovement>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<LoanMovement>>(json) ?? new List<LoanMovement>();
        }
        catch
        {
            return new List<LoanMovement>();
        }
    }

    public IEnumerable<LoanMovement> GetByLoan(Guid loanId) =>
        this.GetAll().Where(m => m.LoanId == loanId);

    public void Add(LoanMovement movement)
    {
        List<LoanMovement> movements = this.GetAll().ToList();
        movements.Add(movement);
        this.WriteAll(movements);
    }

    public void Update(LoanMovement movement)
    {
        List<LoanMovement> movements = this.GetAll().ToList();
        int index = movements.FindIndex(m => m.Id == movement.Id);
        if (index >= 0)
        {
            movements[index] = movement;
            this.WriteAll(movements);
        }
    }

    public bool Delete(Guid id)
    {
        List<LoanMovement> movements = this.GetAll().ToList();
        int removed = movements.RemoveAll(m => m.Id == id);
        if (removed > 0)
        {
            this.WriteAll(movements);
        }

        return removed > 0;
    }

    public int DeleteByYear(int year)
    {
        List<LoanMovement> movements = this.GetAll().ToList();
        int removed = movements.RemoveAll(m => m.Transaction.Date.Year == year);
        if (removed > 0)
        {
            this.WriteAll(movements);
        }

        return removed;
    }

    public void Initialize(IEnumerable<LoanMovement> movements)
    {
        this.WriteAll(movements.ToList());
    }

    private void WriteAll(List<LoanMovement> movements)
    {
        string? dir = Path.GetDirectoryName(this.filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(movements, new JsonSerializerOptions { WriteIndented = true });
        EncryptedJsonFileStorage.WriteText(this.filePath, json, this._vaultService);
    }
}