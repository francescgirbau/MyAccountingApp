using MyAccountingApp.Core.Entities;

namespace MyAccountingApp.Core.Interfaces;

public interface IConversionRepository
{
    void Add(Conversion conversion);

    /// <summary>
    /// Retorna la conversió per data, o null si no existeix
    /// </summary>
    Conversion? GetByDate(DateTime date);

    /// <summary>
    /// Comprova si ja hi ha una conversió guardada per una data concreta
    /// </summary>
    bool ExistsForDate(DateTime date);

    /// <summary>
    /// Retorna totes les conversions guardades (per tests o backup)
    /// </summary>
    IEnumerable<Conversion> GetAll();
}
