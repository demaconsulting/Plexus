package Plexus {
    part def History {
        doc /* Command-based undo/redo history in which every operation is undoable. */

        comment testRef /* Test: test/DemaConsulting.Plexus.Tests/History/HistoryTests.cs */
        comment designRef /* Design: docs/design/plexus/history.md */
        comment verificationRef /* Verification: docs/verification/plexus/history.md */
        comment reqRef /* Requirements: docs/reqstream/plexus/history.yaml */

        part undoHistory : UndoHistory;
    }
}
