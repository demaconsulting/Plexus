package Plexus {
    doc
    /*
     * Plexus is a .NET library template demonstrating DEMA Consulting
     * best practices for structuring, documenting, and testing a DotNet library.
     */

    part def PlexusSystem {
        doc /* The Plexus system as a whole. */

        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/PlexusTests.cs */
        comment designRef /* Design: docs/design/plexus.md */
        comment verificationRef /* Verification: docs/verification/plexus.md */
        comment reqRef /* Requirements: docs/reqstream/plexus.yaml */

        part demo : Demo;
    }
}
