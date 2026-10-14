namespace DemaConsulting.Plexus;

/// <summary>
///     Sets one value of one target and merges with a directly following set of the same value.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <remarks>
///     The command identifies "the same value" by target object and key, so a slider or drag that
///     sets a property repeatedly inside one transaction collapses to a single compact entry that
///     remembers only the first old value and the last new value.
/// </remarks>
internal sealed class SetValueCommand<T> : IMergeableCommand
{
    /// <summary>
    ///     The object whose value is set; used for merge identity.
    /// </summary>
    private readonly object _target;

    /// <summary>
    ///     Identifies the value within the target; used for merge identity.
    /// </summary>
    private readonly string _key;

    /// <summary>
    ///     Applies a value to the target.
    /// </summary>
    private readonly Action<T> _apply;

    /// <summary>
    ///     The value before the change.
    /// </summary>
    private readonly T _oldValue;

    /// <summary>
    ///     The value after the change; replaced when a later command merges in.
    /// </summary>
    private T _newValue;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SetValueCommand{T}"/> class.
    /// </summary>
    /// <param name="description">Human-readable description.</param>
    /// <param name="target">The object whose value is set.</param>
    /// <param name="key">Identifies the value within the target.</param>
    /// <param name="oldValue">The current value.</param>
    /// <param name="newValue">The value to apply.</param>
    /// <param name="apply">Applies a value to the target and raises change notifications.</param>
    public SetValueCommand(string description, object target, string key, T oldValue, T newValue, Action<T> apply)
    {
        Description = description;
        _target = target;
        _key = key;
        _oldValue = oldValue;
        _newValue = newValue;
        _apply = apply;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public void Do() => _apply(_newValue);

    /// <inheritdoc />
    public void Undo() => _apply(_oldValue);

    /// <inheritdoc />
    public bool TryMerge(IUndoableCommand next)
    {
        // Only a set of the very same value on the very same object can be absorbed
        if (next is SetValueCommand<T> other && ReferenceEquals(other._target, _target) && other._key == _key)
        {
            _newValue = other._newValue;
            return true;
        }

        return false;
    }
}
