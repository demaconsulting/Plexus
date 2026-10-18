using System.ComponentModel;

namespace DemaConsulting.Plexus.Avalonia.PropertyGrid.Tests;

/// <summary>
///     Unit tests of the PropertySources unit: reflection, custom, composite and factory sources and ordering.
/// </summary>
public class PropertySourceTests
{
    /// <summary>
    ///     A source over one string value whose commit scope records the steps it performs.
    /// </summary>
    /// <param name="log">Where the steps are recorded.</param>
    private sealed class ScopedSource(List<string> log) : IPropertySource, IPropertyCommitScope
    {
        /// <summary>The underlying items.</summary>
        private readonly CustomPropertySource _inner = new([new CustomPropertyItem("Title", typeof(string), () => "old")]);

        /// <inheritdoc />
        public event EventHandler<PropertySourceChangedEventArgs>? Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        /// <inheritdoc />
        public IReadOnlyList<PropertyItem> GetItems() => _inner.GetItems();

        /// <inheritdoc />
        public void RunCommit(Action apply)
        {
            log.Add("source-before");
            apply();
            log.Add("source-after");
        }
    }

    /// <summary>
    ///     Non-browsable properties are omitted by default and included, flagged, on request.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_GetItems_HidesNonBrowsableByDefault()
    {
        using var source = TestHelpers.PersonSource(out _);
        using var all = new ReflectionPropertySource(new Person(), includeNonBrowsable: true);

        Assert.DoesNotContain(source.GetItems(), i => i.Name == "Secret");
        var hidden = TestHelpers.Item(all, "Secret");
        Assert.False(hidden.IsBrowsable);
        Assert.True(TestHelpers.Item(source, "Name").IsBrowsable);
    }

    /// <summary>
    ///     <c>[DefaultProperty]</c> is exposed by name.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_DefaultProperty_IsExposed()
    {
        using var document = new ReflectionPropertySource(new Document());
        using var person = TestHelpers.PersonSource(out _);

        Assert.Equal("Title", document.DefaultPropertyName);
        Assert.Null(person.DefaultPropertyName);
    }

    /// <summary>
    ///     An object with no public properties has no items and a null target is rejected.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_EmptyOrNullTarget_IsHandled()
    {
        using var source = new ReflectionPropertySource(new Empty());

        Assert.Empty(source.GetItems());
        Assert.Throws<ArgumentNullException>(() => new ReflectionPropertySource(null!));
    }

    /// <summary>
    ///     <see cref="ICustomTypeDescriptor"/> properties are shown and editable, and a refresh picks up new ones.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_CustomTypeDescriptor_ExposesDynamicProperties()
    {
        var bag = new DynamicBag();
        bag.Define("Speed", 3);
        bag.Define("Label", "fast");
        using var source = new ReflectionPropertySource(bag);
        var refreshed = 0;
        source.Changed += (_, e) => refreshed += e.IsAll ? 1 : 0;

        Assert.Equal(["Label", "Speed"], source.GetItems().Select(i => i.Name).Order());
        var speed = TestHelpers.Item(source, "Speed");
        Assert.Equal("Dynamic", speed.Category);
        Assert.Equal("Defined at run time.", speed.Description);
        Assert.True(speed.TrySetText("9").Succeeded);
        Assert.Equal(9, bag.Read("Speed"));

        bag.Define("Extra", 1.5);
        Assert.Equal(2, source.GetItems().Count);
        source.Refresh();
        Assert.Equal(3, source.GetItems().Count);
        Assert.Equal(1, refreshed);
    }

    /// <summary>
    ///     A <see cref="TypeDescriptionProvider"/> that adds a property is honored.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_TypeDescriptionProvider_AddsProperties()
    {
        var provider = new ExtraPropertyProvider();
        TypeDescriptor.AddProvider(provider, typeof(Provided));
        try
        {
            using var source = new ReflectionPropertySource(new Provided());

            Assert.Equal(["Extra", "Real"], source.GetItems().Select(i => i.Name).Order());
            var extra = TestHelpers.Item(source, "Extra");
            Assert.True(extra.IsReadOnly);
            Assert.Equal("extra", extra.GetValue());
        }
        finally
        {
            TypeDescriptor.RemoveProvider(provider, typeof(Provided));
        }
    }

    /// <summary>
    ///     A target that raises <see cref="INotifyPropertyChanged"/> makes the source raise
    ///     <see cref="IPropertySource.Changed"/>; disposing detaches the source.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_NotifyPropertyChanged_RaisesChanged()
    {
        var target = new Observable();
        var source = new ReflectionPropertySource(target);
        var names = new List<string?>();
        source.Changed += (_, e) => names.Add(e.PropertyName);

        target.Title = "one";
        target.NotifyAll();
        source.Dispose();
        target.Title = "two";

        Assert.Equal(["Title", null], names);
    }

    /// <summary>
    ///     A change of an observable property annotated <c>RefreshProperties.All</c> rebuilds the item list and raises an
    ///     all-items notification; an explicit refresh does the same.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_RefreshAllProperty_RebuildsItems()
    {
        // Arrange
        var target = new Refreshing();
        using var source = new ReflectionPropertySource(target);
        var before = TestHelpers.Item(source, "Other");
        var notifications = new List<PropertySourceChangedEventArgs>();
        source.Changed += (_, e) => notifications.Add(e);

        // Act: the change notification of the refresh-all property
        target.Mode = 7;
        var afterChange = TestHelpers.Item(source, "Other");

        // Assert
        Assert.NotSame(before, afterChange);
        Assert.Equal(7, TestHelpers.Item(source, "Mode").GetValue());
        var notification = Assert.Single(notifications);
        Assert.True(notification.IsAll);

        // Act: an explicit refresh
        source.Refresh();

        // Assert
        Assert.NotSame(afterChange, TestHelpers.Item(source, "Other"));
        Assert.Equal(2, notifications.Count);
        Assert.True(notifications[1].IsAll);
    }

    /// <summary>
    ///     A notification of a shown property annotated <c>RefreshProperties.All</c> makes the source rebuild its items
    ///     and report a change of everything; an ordinary property only reports its own name.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_ShownRefreshAllPropertyChanged_RebuildsItems()
    {
        // Arrange
        var target = new Refreshing();
        using var source = new ReflectionPropertySource(target);
        var before = TestHelpers.Item(source, "Other");
        var notifications = new List<PropertySourceChangedEventArgs>();
        source.Changed += (_, e) => notifications.Add(e);

        // Act
        target.Other = 1;
        var afterOrdinary = TestHelpers.Item(source, "Other");
        target.Mode = 2;
        var afterRefreshAll = TestHelpers.Item(source, "Other");

        // Assert: the ordinary change keeps the items, the refresh-all change replaces them and reports everything
        Assert.Same(before, afterOrdinary);
        Assert.NotSame(before, afterRefreshAll);
        Assert.Equal(2, notifications.Count);
        Assert.Equal("Other", notifications[0].PropertyName);
        Assert.True(notifications[1].IsAll);
    }

    /// <summary>
    ///     A notification of a hidden property annotated <c>RefreshProperties.All</c> also rebuilds the items, although that
    ///     property has no item.
    /// </summary>
    [Fact]
    public void ReflectionPropertySource_HiddenRefreshAllPropertyChanged_RebuildsItems()
    {
        // Arrange
        var target = new HiddenRefreshing();
        using var source = new ReflectionPropertySource(target);
        var before = TestHelpers.Item(source, "Shown");
        var notifications = new List<PropertySourceChangedEventArgs>();
        source.Changed += (_, e) => notifications.Add(e);

        // Act
        target.Mode = 1;

        // Assert
        Assert.DoesNotContain(source.GetItems(), i => i.Name == "Mode");
        Assert.NotSame(before, TestHelpers.Item(source, "Shown"));
        var item = Assert.Single(notifications);
        Assert.True(item.IsAll);
    }

    /// <summary>
    ///     A composite source concatenates its sources in order and forwards their changes.
    /// </summary>
    [Fact]
    public void CompositePropertySource_GetItems_MergesInOrderAndForwardsChanges()
    {
        var first = new CustomPropertySource([new CustomPropertyItem("A", typeof(int), () => 1)]);
        var second = new CustomPropertySource([new CustomPropertyItem("B", typeof(int), () => 2)]);
        using var composite = new CompositePropertySource(first, second);
        var changes = new List<string?>();
        composite.Changed += (_, e) => changes.Add(e.PropertyName);

        second.NotifyValueChanged("B");

        Assert.Equal(["A", "B"], composite.GetItems().Select(i => i.Name));
        Assert.Equal(["B"], changes);
        Assert.Equal(2, composite.Sources.Count);
    }

    /// <summary>
    ///     When two sources supply the same name the earlier source's item is kept.
    /// </summary>
    [Fact]
    public void CompositePropertySource_GetItems_DuplicateNameKeepsTheFirst()
    {
        var first = new CustomPropertySource([new CustomPropertyItem("Name", typeof(string), () => "first")]);
        var second = new CustomPropertySource([new CustomPropertyItem("Name", typeof(string), () => "second")]);
        using var composite = new CompositePropertySource(new IPropertySource[] { first, second }.AsEnumerable());

        var item = Assert.Single(composite.GetItems());

        Assert.Equal("first", item.GetValue());
    }

    /// <summary>
    ///     Adding and removing sources changes the items and raises a change for everything; disposing detaches.
    /// </summary>
    [Fact]
    public void CompositePropertySource_AddRemoveDispose_UpdateItemsAndSubscriptions()
    {
        var extra = new CustomPropertySource([new CustomPropertyItem("X", typeof(int), () => 1)]);
        var composite = new CompositePropertySource();
        var all = 0;
        composite.Changed += (_, e) => all += e.IsAll ? 1 : 0;

        composite.Add(extra);
        Assert.Single(composite.GetItems());
        Assert.True(composite.Remove(extra));
        Assert.False(composite.Remove(extra));
        Assert.Empty(composite.GetItems());
        Assert.Equal(2, all);

        composite.Add(extra);
        composite.Dispose();
        extra.NotifyValueChanged("X");
        Assert.Equal(3, all);
        Assert.Throws<ArgumentNullException>(() => composite.Add(null!));
    }

    /// <summary>
    ///     A composite of a reflection source and a custom source shows both and edits through either.
    /// </summary>
    [Fact]
    public void CompositePropertySource_ReflectionAndCustom_AreEditedThroughTheirOwnItems()
    {
        var person = new Person { Name = "Ada" };
        var title = "node";
        using var reflection = new ReflectionPropertySource(person);
        var custom = new CustomPropertySource(
            [new CustomPropertyItem("NodeTitle", typeof(string), () => title, v => title = (string)v!)]);
        using var composite = new CompositePropertySource(custom, reflection);

        Assert.Equal("NodeTitle", composite.GetItems()[0].Name);
        Assert.True(TestHelpers.Item(composite, "NodeTitle").TrySetText("renamed").Succeeded);
        Assert.True(TestHelpers.Item(composite, "Name").TrySetText("Grace").Succeeded);

        Assert.Equal("renamed", title);
        Assert.Equal("Grace", person.Name);
    }

    /// <summary>
    ///     A custom source returns only browsable items and raises changes for list and value changes.
    /// </summary>
    [Fact]
    public void CustomPropertySource_AddRemoveNotify_RaiseChanges()
    {
        var source = new CustomPropertySource();
        var changes = new List<string?>();
        source.Changed += (_, e) => changes.Add(e.PropertyName);
        var visible = new CustomPropertyItem("V", typeof(int), () => 1);
        var hidden = new CustomPropertyItem("H", typeof(int), () => 2, null, new CustomPropertyOptions { IsBrowsable = false });

        source.Add(visible);
        source.Add(hidden);
        source.NotifyValueChanged("V");
        source.Refresh();
        Assert.True(source.Remove(hidden));
        Assert.False(source.Remove(hidden));

        Assert.Equal(["V"], source.GetItems().Select(i => i.Name));
        Assert.Equal([null, null, "V", null, null], changes);
        Assert.Throws<ArgumentNullException>(() => source.Add(null!));
    }

    /// <summary>
    ///     A custom item carries its metadata, defaults and read-only state, and validates its arguments.
    /// </summary>
    [Fact]
    public void CustomPropertyItem_Options_AreExposed()
    {
        var value = 3.0;
        var item = new CustomPropertyItem(
            "Gain",
            typeof(double),
            () => value,
            v => value = (double)v!,
            new CustomPropertyOptions
            {
                DisplayName = "Gain (dB)",
                Category = "Audio",
                Description = "Amplification.",
                HasDefault = true,
                DefaultValue = 0.0,
                IsPasswordText = true,
                IsParenthesized = true,
                RefreshMode = RefreshProperties.Repaint,
                EditorKey = "knob",
                Converter = new DoubleConverter(),
            });
        var plain = new CustomPropertyItem("Plain", typeof(int), () => 1);
        var forced = new CustomPropertyItem("Forced", typeof(int), () => 1, _ => { }, new CustomPropertyOptions { ForceReadOnly = true });

        Assert.Equal("(Gain (dB))", item.Label);
        Assert.Equal("Audio", item.Category);
        Assert.Equal("Amplification.", item.Description);
        Assert.True(item.IsPasswordText);
        Assert.Equal(RefreshProperties.Repaint, item.RefreshMode);
        Assert.Equal("knob", item.EditorKey);
        Assert.IsType<DoubleConverter>(item.Converter);
        Assert.True(item.IsChanged);
        Assert.True(item.TryReset().Succeeded);
        Assert.Equal(0.0, value);
        Assert.False(item.IsChanged);
        Assert.Equal("Misc", plain.Category);
        Assert.Equal("Plain", plain.DisplayName);
        Assert.True(plain.IsReadOnly);
        Assert.True(forced.IsReadOnly);
        Assert.False(plain.TrySetValue(2).Succeeded);
        Assert.Throws<NotSupportedException>(() => plain.SetValue(2));
        Assert.Throws<ArgumentException>(() => new CustomPropertyItem(" ", typeof(int), () => 1));
        Assert.Throws<ArgumentNullException>(() => new CustomPropertyItem("n", null!, () => 1));
        Assert.Throws<ArgumentNullException>(() => new CustomPropertyItem("n", typeof(int), null!));
    }

    /// <summary>
    ///     The factory picks reflection by default, an object's own source or provider, and a registered adapter first.
    /// </summary>
    [Fact]
    public void PropertySourceFactory_Create_ChoosesRegisteredProviderOrReflection()
    {
        var target = new Gauge();
        var own = new CustomPropertySource();

        using var reflected = Assert.IsType<ReflectionPropertySource>(PropertySourceFactory.Create(new Person()));
        Assert.Same(own, PropertySourceFactory.Create(own));
        var provided = PropertySourceFactory.Create(target);
        Assert.Equal("Value", Assert.Single(provided.GetItems()).Name);

        using (PropertySourceFactory.Register(t => t is Person ? own : null))
        {
            Assert.Same(own, PropertySourceFactory.Create(new Person()));
            Assert.IsType<CustomPropertySource>(PropertySourceFactory.Create(target));
        }

        using var again = Assert.IsType<ReflectionPropertySource>(PropertySourceFactory.Create(new FactoryProbe()));
        Assert.Throws<ArgumentNullException>(() => PropertySourceFactory.Create(null!));
        Assert.Throws<ArgumentNullException>(() => PropertySourceFactory.Register(null!));
    }

    /// <summary>
    ///     Categorized ordering sorts by category then label, with parenthesized names first, ignoring case.
    /// </summary>
    [Fact]
    public void PropertyOrdering_Order_CategorizedSortsByCategoryThenLabel()
    {
        using var source = TestHelpers.PersonSource(out _);

        var ordered = PropertyOrdering.Order(source.GetItems(), PropertySortMode.Categorized);

        var names = ordered.Select(i => i.Name).ToList();
        Assert.True(names.IndexOf("Handle") < names.IndexOf("Active"), "Parenthesized names come first within their category");
        var identity = ordered.Where(i => i.Category == "Identity").Select(i => i.DisplayName).ToList();
        Assert.Equal(["Age", "Full Name"], identity);
        var categories = ordered.Select(i => i.Category).Distinct().ToList();
        Assert.Equal(categories.Order(StringComparer.InvariantCultureIgnoreCase), categories);
    }

    /// <summary>
    ///     Alphabetical ordering ignores categories and puts parenthesized names first.
    /// </summary>
    [Fact]
    public void PropertyOrdering_Order_AlphabeticalIgnoresCategories()
    {
        using var source = TestHelpers.PersonSource(out _);

        var ordered = PropertyOrdering.Order(source.GetItems(), PropertySortMode.Alphabetical);

        Assert.Equal("Handle", ordered[0].Name);
        var rest = ordered.Skip(1).Select(i => i.DisplayName).ToList();
        Assert.Equal(rest.Order(StringComparer.InvariantCultureIgnoreCase), rest);
        Assert.Throws<ArgumentNullException>(() => PropertyOrdering.Order(null!, PropertySortMode.Alphabetical));
    }

    /// <summary>
    ///     Grouping yields one group per category (categories differing only in case share a group) or one flat group.
    /// </summary>
    [Fact]
    public void PropertyOrdering_Group_ProducesCategoryGroupsOrOneFlatGroup()
    {
        var items = new[]
        {
            new CustomPropertyItem("b", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Zed" }),
            new CustomPropertyItem("a", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "alpha" }),
            new CustomPropertyItem("c", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Alpha" }),
        };

        var grouped = PropertyOrdering.Group(items, PropertySortMode.Categorized);
        var flat = PropertyOrdering.Group(items, PropertySortMode.Alphabetical);

        Assert.Equal(2, grouped.Count);
        Assert.Equal(["a", "c"], grouped[0].Items.Select(i => i.Name));
        Assert.Equal("alpha", grouped[0].Category);
        Assert.Equal("Zed", grouped[1].Category);
        var single = Assert.Single(flat);
        Assert.Null(single.Category);
        Assert.Equal(["a", "b", "c"], single.Items.Select(i => i.Name));
        Assert.Empty(PropertyOrdering.Group([], PropertySortMode.Categorized));
    }

    /// <summary>
    ///     Categorized ordering puts parenthesized names first, then sorts a category's items by their rank, ties alphabetically.
    /// </summary>
    [Fact]
    public void PropertyOrdering_Order_CategorizedSortsByItemOrderThenLabel()
    {
        // Arrange: ranks out of alphabetical order, a tie, a parenthesized item with a high rank and a default-rank item
        var items = new[]
        {
            new CustomPropertyItem("Release", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Envelope", Order = 4 }),
            new CustomPropertyItem("Attack", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Envelope", Order = 1 }),
            new CustomPropertyItem("Sustain", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Envelope", Order = 2 }),
            new CustomPropertyItem("Decay", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Envelope", Order = 2 }),
            new CustomPropertyItem("Zero", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Envelope" }),
            new CustomPropertyItem("(Name)", typeof(int), () => 1, null, new CustomPropertyOptions { Category = "Envelope", Order = 9, IsParenthesized = true }),
        };

        // Act
        var ordered = PropertyOrdering.Order(items, PropertySortMode.Categorized);

        // Assert: parenthesized first, then ascending rank (0 before 1), equal ranks alphabetical
        Assert.Equal(["(Name)", "Zero", "Attack", "Decay", "Sustain", "Release"], ordered.Select(i => i.Name));
        Assert.Equal(4, items[0].Order);
    }

    /// <summary>
    ///     Alphabetical ordering ignores item ranks.
    /// </summary>
    [Fact]
    public void PropertyOrdering_Order_AlphabeticalIgnoresItemOrder()
    {
        // Arrange
        var items = new[]
        {
            new CustomPropertyItem("Attack", typeof(int), () => 1, null, new CustomPropertyOptions { Order = 3 }),
            new CustomPropertyItem("Decay", typeof(int), () => 1, null, new CustomPropertyOptions { Order = 2 }),
            new CustomPropertyItem("Release", typeof(int), () => 1, null, new CustomPropertyOptions { Order = 1 }),
        };

        // Act
        var ordered = PropertyOrdering.Order(items, PropertySortMode.Alphabetical);

        // Assert
        Assert.Equal(["Attack", "Decay", "Release"], ordered.Select(i => i.Name));
    }

    /// <summary>
    ///     A category with a lower rank is shown first; within a rank the order is alphabetical, and a category ranks as its lowest item.
    /// </summary>
    [Fact]
    public void PropertyOrdering_Group_OrdersCategoriesByRankThenAlphabetically()
    {
        // Arrange
        PropertyItem Make(string name, string category, int order) =>
            new CustomPropertyItem(name, typeof(string), () => string.Empty, null, new CustomPropertyOptions { Category = category, CategoryOrder = order });
        var items = new[]
        {
            Make("A", "Alpha", 0),
            Make("B", "Zulu", -1),
            Make("C", "Mike", 0),
            Make("D", "Zulu", 5),
        };

        // Act
        var groups = PropertyOrdering.Group(items, PropertySortMode.Categorized);

        // Assert
        Assert.Equal(["Zulu", "Alpha", "Mike"], groups.Select(g => g.Category));
        Assert.Equal(["B", "D"], groups[0].Items.Select(i => i.Name));
        Assert.Equal(0, new CustomPropertyOptions().CategoryOrder);
    }

    /// <summary>
    ///     A composite chains the scopes of its sources, the first source outermost, and runs the action once.
    /// </summary>
    [Fact]
    public void CompositePropertySource_RunCommit_ChainsTheSourceScopes()
    {
        // Arrange
        var log = new List<string>();
        var first = new ScopedSource(log);
        var second = new ScopedSource(log);
        var plain = new CustomPropertySource();
        using var composite = new CompositePropertySource(first, plain, second);

        // Act
        composite.RunCommit(() => log.Add("apply"));

        // Assert
        Assert.Equal(["source-before", "source-before", "apply", "source-after", "source-after"], log);
        Assert.Throws<ArgumentNullException>(() => composite.RunCommit(null!));
    }
}

/// <summary>
///     An object that supplies its own property source.
/// </summary>
public sealed class Gauge : IPropertySourceProvider
{
    /// <summary>Gets or sets the value.</summary>
    public double Value { get; set; }

    /// <inheritdoc />
    public IPropertySource CreatePropertySource() =>
        new CustomPropertySource([new CustomPropertyItem("Value", typeof(double), () => Value, v => Value = (double)v!)]);
}

/// <summary>
///     An object only the factory test registers an adapter for.
/// </summary>
#pragma warning disable S2094 // A marker type
public sealed class FactoryProbe;
#pragma warning restore S2094
