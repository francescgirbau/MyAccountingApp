namespace MyAccountingApp.Web.Components;

/// <summary>Input payload raised by the split/reverse-split dialog for preview and apply.</summary>
public sealed record SplitAdjustmentInput(
    string Symbol,
    decimal Factor,
    DateTime? AsOfDate);