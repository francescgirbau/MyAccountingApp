using Microsoft.JSInterop;

namespace MyAccountingApp.Web.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IJSRuntime"/> stand-in for the theme storage calls: records every
/// invocation and answers with whatever the test configured, including failures.
/// </summary>
internal sealed class FakeJSRuntime : IJSRuntime
{
    private readonly Dictionary<string, object?> _returnValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<object?[]>> _calls = new(StringComparer.Ordinal);
    private Exception? _failure;

    public IReadOnlyList<object?[]> CallsTo(string identifier)
    {
        return this._calls.TryGetValue(identifier, out List<object?[]>? calls)
            ? calls
            : new List<object?[]>();
    }

    public FakeJSRuntime Returns(string identifier, object? value)
    {
        this._returnValues[identifier] = value;
        return this;
    }

    public FakeJSRuntime Fails(string identifier, Exception failure)
    {
        this._failures[identifier] = failure;
        return this;
    }

    public FakeJSRuntime FailsEverything(Exception failure)
    {
        this._failure = failure;
        return this;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return this.InvokeAsync<TValue>(identifier, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (!this._calls.TryGetValue(identifier, out List<object?[]>? calls))
        {
            calls = new List<object?[]>();
            this._calls[identifier] = calls;
        }

        calls.Add(args ?? Array.Empty<object?>());

        if (this._failure is not null)
        {
            throw this._failure;
        }

        if (this._failures.TryGetValue(identifier, out Exception? failure))
        {
            throw failure;
        }

        object? value = this._returnValues.GetValueOrDefault(identifier);
        return ValueTask.FromResult(value is null ? default! : (TValue)value);
    }
}
