package Plexus {
    private import SoftwareItemMetadata::*;

    part def History {
        doc /* Command-based undo/redo history in which every operation is undoable. */

        metadata info : SoftwareInfo {
            description = "Command-based undo/redo history in which every operation is undoable.";
            testFiles = ("test/DemaConsulting.Plexus.Tests/History/HistoryTests.cs");
            designFiles = ("docs/design/plexus/history.md");
            verificationFiles = ("docs/verification/plexus/history.md");
            requirementFiles = ("docs/reqstream/plexus/history.yaml");
        }

        part undoHistory : UndoHistory;
    }
}
