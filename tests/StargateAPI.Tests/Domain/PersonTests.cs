using StargateAPI.Domain;

namespace StargateAPI.Tests.Domain;

public class PersonTests
{
    [Fact]
    public void A_version_check_fails_only_for_a_supplied_version_that_is_no_longer_current()
    {
        var person = Person.Create("Grace Hopper");
        var read = person.Version;

        Assert.False(person.IsStale(read));
        Assert.False(person.IsStale(null));

        person.Touch();
        Assert.True(person.IsStale(read));
        Assert.False(person.IsStale(null));
    }

    [Fact]
    public void Replacing_names_reports_exactly_the_fields_that_changed_and_bumps_the_version()
    {
        var person = Person.Create("Grace Hopper", "Grace", null, "Hopper");
        var before = person.Version;

        var changed = person.ReplaceNames("Grace B. Hopper", "Grace", " Brewster ", null);

        Assert.Equal(["name", "middleName", "lastName"], changed);
        Assert.Equal(("Grace B. Hopper", "Grace", "Brewster", (string?)null),
            (person.Name, person.FirstName, person.MiddleName, person.LastName));
        Assert.NotEqual(before, person.Version);
    }

    [Fact]
    public void Replacing_names_with_the_same_values_is_a_no_op_that_keeps_the_version()
    {
        var person = Person.Create("Grace Hopper", "Grace", null, "Hopper");
        var before = person.Version;

        // Whitespace differences and blank parts normalize to the stored values.
        var changed = person.ReplaceNames(" Grace Hopper ", "Grace ", "   ", "Hopper");

        Assert.Empty(changed);
        Assert.Equal(before, person.Version);
    }
}
