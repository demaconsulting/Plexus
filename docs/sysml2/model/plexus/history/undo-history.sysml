package Plexus {
    private import SoftwareItemMetadata::*;

    part def UndoHistory {
        doc /* Per-diagram undo and redo stacks, transactions, mergeable commands and dirty tracking. */

        metadata info : SoftwareInfo {
            description = "Per-diagram undo and redo stacks, transactions, mergeable commands and dirty tracking.";
            sourceFiles = ("src/DemaConsulting.Plexus/History/CommandPlan.cs", "src/DemaConsulting.Plexus/History/CompositeCommand.cs", "src/DemaConsulting.Plexus/History/DelegateCommand.cs", "src/DemaConsulting.Plexus/History/HistoryTransaction.cs", "src/DemaConsulting.Plexus/History/IMergeableCommand.cs", "src/DemaConsulting.Plexus/History/IUndoableCommand.cs", "src/DemaConsulting.Plexus/History/SetValueCommand.cs", "src/DemaConsulting.Plexus/History/UndoHistory.cs");
            testFiles = ("test/DemaConsulting.Plexus.Tests/UndoHistoryTests.cs");
            designFiles = ("docs/design/plexus/history/undo-history.md");
            verificationFiles = ("docs/verification/plexus/history/undo-history.md");
            requirementFiles = ("docs/reqstream/plexus/history/undo-history.yaml");
        }
    }
}
