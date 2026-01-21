using FluentAssertions;
using MusicEngine.Core.UndoRedo;
using Moq;
using Xunit;

namespace MusicEngine.Tests.Core;

public class UndoRedoTests
{
    #region UndoManager Tests

    [Fact]
    public void UndoManager_Constructor_InitializesWithDefaults()
    {
        var manager = new UndoManager();

        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
        manager.UndoCount.Should().Be(0);
        manager.RedoCount.Should().Be(0);
        manager.MaxHistorySize.Should().Be(100);
    }

    [Fact]
    public void UndoManager_Constructor_WithCustomHistorySize()
    {
        var manager = new UndoManager(50);

        manager.MaxHistorySize.Should().Be(50);
    }

    [Fact]
    public void UndoManager_Constructor_ThrowsOnInvalidHistorySize()
    {
        var action = () => new UndoManager(0);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UndoManager_Constructor_ThrowsOnNegativeHistorySize()
    {
        var action = () => new UndoManager(-1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UndoManager_Execute_AddsToUndoStack()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test Command");

        manager.Execute(command);

        manager.CanUndo.Should().BeTrue();
        manager.UndoCount.Should().Be(1);
        manager.NextUndoDescription.Should().Be("Test Command");
    }

    [Fact]
    public void UndoManager_Execute_CallsCommandExecute()
    {
        var manager = new UndoManager();
        var mockCommand = new Mock<IUndoableCommand>();
        mockCommand.Setup(c => c.Description).Returns("Test");

        manager.Execute(mockCommand.Object);

        mockCommand.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void UndoManager_Execute_ClearsRedoStack()
    {
        var manager = new UndoManager();
        var command1 = CreateMockCommand("Command 1");
        var command2 = CreateMockCommand("Command 2");

        manager.Execute(command1);
        manager.Undo();
        manager.CanRedo.Should().BeTrue();

        manager.Execute(command2);

        manager.CanRedo.Should().BeFalse();
        manager.RedoCount.Should().Be(0);
    }

    [Fact]
    public void UndoManager_Execute_ThrowsOnNullCommand()
    {
        var manager = new UndoManager();

        var action = () => manager.Execute(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UndoManager_Execute_FiresCommandExecutingEvent()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");
        CommandEventArgs? eventArgs = null;

        manager.CommandExecuting += (s, e) => eventArgs = e;
        manager.Execute(command);

        eventArgs.Should().NotBeNull();
        eventArgs!.Command.Should().BeSameAs(command);
        eventArgs.Action.Should().Be(CommandAction.Execute);
    }

    [Fact]
    public void UndoManager_Execute_FiresCommandExecutedEvent()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");
        CommandEventArgs? eventArgs = null;

        manager.CommandExecuted += (s, e) => eventArgs = e;
        manager.Execute(command);

        eventArgs.Should().NotBeNull();
        eventArgs!.Command.Should().BeSameAs(command);
        eventArgs.Action.Should().Be(CommandAction.Execute);
    }

    [Fact]
    public void UndoManager_Execute_FiresStateChangedEvent()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");
        bool stateChanged = false;

        manager.StateChanged += (s, e) => stateChanged = true;
        manager.Execute(command);

        stateChanged.Should().BeTrue();
    }

    [Fact]
    public void UndoManager_Undo_RemovesFromUndoStack()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");

        manager.Execute(command);
        var result = manager.Undo();

        result.Should().BeTrue();
        manager.CanUndo.Should().BeFalse();
        manager.UndoCount.Should().Be(0);
    }

    [Fact]
    public void UndoManager_Undo_AddsToRedoStack()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");

        manager.Execute(command);
        manager.Undo();

        manager.CanRedo.Should().BeTrue();
        manager.RedoCount.Should().Be(1);
        manager.NextRedoDescription.Should().Be("Test");
    }

    [Fact]
    public void UndoManager_Undo_CallsCommandUndo()
    {
        var manager = new UndoManager();
        var mockCommand = new Mock<IUndoableCommand>();
        mockCommand.Setup(c => c.Description).Returns("Test");

        manager.Execute(mockCommand.Object);
        manager.Undo();

        mockCommand.Verify(c => c.Undo(), Times.Once);
    }

    [Fact]
    public void UndoManager_Undo_ReturnsFalseWhenEmpty()
    {
        var manager = new UndoManager();

        var result = manager.Undo();

        result.Should().BeFalse();
    }

    [Fact]
    public void UndoManager_Undo_FiresStateChangedEvent()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");
        manager.Execute(command);

        bool stateChanged = false;
        manager.StateChanged += (s, e) => stateChanged = true;
        manager.Undo();

        stateChanged.Should().BeTrue();
    }

    [Fact]
    public void UndoManager_Redo_RemovesFromRedoStack()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");

        manager.Execute(command);
        manager.Undo();
        var result = manager.Redo();

        result.Should().BeTrue();
        manager.CanRedo.Should().BeFalse();
        manager.RedoCount.Should().Be(0);
    }

    [Fact]
    public void UndoManager_Redo_AddsToUndoStack()
    {
        var manager = new UndoManager();
        var command = CreateMockCommand("Test");

        manager.Execute(command);
        manager.Undo();
        manager.Redo();

        manager.CanUndo.Should().BeTrue();
        manager.UndoCount.Should().Be(1);
    }

    [Fact]
    public void UndoManager_Redo_CallsCommandRedo()
    {
        var manager = new UndoManager();
        var mockCommand = new Mock<IUndoableCommand>();
        mockCommand.Setup(c => c.Description).Returns("Test");

        manager.Execute(mockCommand.Object);
        manager.Undo();
        manager.Redo();

        mockCommand.Verify(c => c.Redo(), Times.Once);
    }

    [Fact]
    public void UndoManager_Redo_ReturnsFalseWhenEmpty()
    {
        var manager = new UndoManager();

        var result = manager.Redo();

        result.Should().BeFalse();
    }

    [Fact]
    public void UndoManager_Clear_ClearsAllStacks()
    {
        var manager = new UndoManager();
        manager.Execute(CreateMockCommand("Command 1"));
        manager.Execute(CreateMockCommand("Command 2"));
        manager.Undo();

        manager.Clear();

        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
        manager.UndoCount.Should().Be(0);
        manager.RedoCount.Should().Be(0);
    }

    [Fact]
    public void UndoManager_Clear_FiresStateChangedEvent()
    {
        var manager = new UndoManager();
        manager.Execute(CreateMockCommand("Test"));

        bool stateChanged = false;
        manager.StateChanged += (s, e) => stateChanged = true;
        manager.Clear();

        stateChanged.Should().BeTrue();
    }

    [Fact]
    public void UndoManager_GetUndoHistory_ReturnsDescriptions()
    {
        var manager = new UndoManager();
        manager.Execute(CreateMockCommand("Command 1"));
        manager.Execute(CreateMockCommand("Command 2"));
        manager.Execute(CreateMockCommand("Command 3"));

        var history = manager.GetUndoHistory();

        history.Should().HaveCount(3);
        history[0].Should().Be("Command 3");
        history[1].Should().Be("Command 2");
        history[2].Should().Be("Command 1");
    }

    [Fact]
    public void UndoManager_GetRedoHistory_ReturnsDescriptions()
    {
        var manager = new UndoManager();
        manager.Execute(CreateMockCommand("Command 1"));
        manager.Execute(CreateMockCommand("Command 2"));
        manager.Undo();
        manager.Undo();

        var history = manager.GetRedoHistory();

        history.Should().HaveCount(2);
        history[0].Should().Be("Command 1");
        history[1].Should().Be("Command 2");
    }

    [Fact]
    public void UndoManager_NextUndoDescription_ReturnsNullWhenEmpty()
    {
        var manager = new UndoManager();

        manager.NextUndoDescription.Should().BeNull();
    }

    [Fact]
    public void UndoManager_NextRedoDescription_ReturnsNullWhenEmpty()
    {
        var manager = new UndoManager();

        manager.NextRedoDescription.Should().BeNull();
    }

    #endregion

    #region History Limit Tests

    [Fact]
    public void UndoManager_HistoryLimit_TrimsOldCommands()
    {
        var manager = new UndoManager(3);

        manager.Execute(CreateMockCommand("Command 1"));
        manager.Execute(CreateMockCommand("Command 2"));
        manager.Execute(CreateMockCommand("Command 3"));
        manager.Execute(CreateMockCommand("Command 4"));

        manager.UndoCount.Should().Be(3);
        var history = manager.GetUndoHistory();
        history.Should().NotContain("Command 1");
        history.Should().Contain("Command 4");
    }

    [Fact]
    public void UndoManager_HistoryLimit_PreservesRecentCommands()
    {
        var manager = new UndoManager(2);

        manager.Execute(CreateMockCommand("Command 1"));
        manager.Execute(CreateMockCommand("Command 2"));
        manager.Execute(CreateMockCommand("Command 3"));

        var history = manager.GetUndoHistory();
        history.Should().HaveCount(2);
        history[0].Should().Be("Command 3");
        history[1].Should().Be("Command 2");
    }

    #endregion

    #region CompositeCommand Tests

    [Fact]
    public void CompositeCommand_Constructor_SetsDescription()
    {
        var composite = new CompositeCommand("Test Composite");

        composite.Description.Should().Be("Test Composite");
    }

    [Fact]
    public void CompositeCommand_Constructor_ThrowsOnNullDescription()
    {
        var action = () => new CompositeCommand(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CompositeCommand_Add_AddsCommand()
    {
        var composite = new CompositeCommand("Test");
        var command = CreateMockCommand("Command 1");

        composite.Add(command);

        composite.Commands.Should().HaveCount(1);
        composite.Commands[0].Should().BeSameAs(command);
    }

    [Fact]
    public void CompositeCommand_Add_ThrowsOnNullCommand()
    {
        var composite = new CompositeCommand("Test");

        var action = () => composite.Add(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CompositeCommand_Execute_ExecutesAllCommandsInOrder()
    {
        var composite = new CompositeCommand("Test");
        var executionOrder = new List<int>();

        var mock1 = new Mock<IUndoableCommand>();
        mock1.Setup(c => c.Execute()).Callback(() => executionOrder.Add(1));
        mock1.Setup(c => c.Description).Returns("Command 1");

        var mock2 = new Mock<IUndoableCommand>();
        mock2.Setup(c => c.Execute()).Callback(() => executionOrder.Add(2));
        mock2.Setup(c => c.Description).Returns("Command 2");

        composite.Add(mock1.Object);
        composite.Add(mock2.Object);

        composite.Execute();

        executionOrder.Should().ContainInOrder(1, 2);
    }

    [Fact]
    public void CompositeCommand_Undo_UndoesAllCommandsInReverseOrder()
    {
        var composite = new CompositeCommand("Test");
        var undoOrder = new List<int>();

        var mock1 = new Mock<IUndoableCommand>();
        mock1.Setup(c => c.Undo()).Callback(() => undoOrder.Add(1));
        mock1.Setup(c => c.Description).Returns("Command 1");

        var mock2 = new Mock<IUndoableCommand>();
        mock2.Setup(c => c.Undo()).Callback(() => undoOrder.Add(2));
        mock2.Setup(c => c.Description).Returns("Command 2");

        composite.Add(mock1.Object);
        composite.Add(mock2.Object);

        composite.Undo();

        undoOrder.Should().ContainInOrder(2, 1);
    }

    [Fact]
    public void CompositeCommand_Redo_RedoesAllCommandsInOrder()
    {
        var composite = new CompositeCommand("Test");
        var redoOrder = new List<int>();

        var mock1 = new Mock<IUndoableCommand>();
        mock1.Setup(c => c.Redo()).Callback(() => redoOrder.Add(1));
        mock1.Setup(c => c.Description).Returns("Command 1");

        var mock2 = new Mock<IUndoableCommand>();
        mock2.Setup(c => c.Redo()).Callback(() => redoOrder.Add(2));
        mock2.Setup(c => c.Description).Returns("Command 2");

        composite.Add(mock1.Object);
        composite.Add(mock2.Object);

        composite.Redo();

        redoOrder.Should().ContainInOrder(1, 2);
    }

    [Fact]
    public void CompositeCommand_Commands_ReturnsReadOnlyList()
    {
        var composite = new CompositeCommand("Test");
        composite.Add(CreateMockCommand("Command 1"));

        var commands = composite.Commands;

        commands.Should().BeAssignableTo<IReadOnlyList<IUndoableCommand>>();
    }

    #endregion

    #region PropertyChangeCommand Tests

    [Fact]
    public void PropertyChangeCommand_Constructor_SetsProperties()
    {
        var command = new PropertyChangeCommand<int>("TestProperty", _ => { }, 10, 20);

        command.Description.Should().Be("Change TestProperty");
        command.OldValue.Should().Be(10);
        command.NewValue.Should().Be(20);
    }

    [Fact]
    public void PropertyChangeCommand_Execute_SetsNewValue()
    {
        int currentValue = 10;
        var command = new PropertyChangeCommand<int>("TestProperty", v => currentValue = v, 10, 20);

        command.Execute();

        currentValue.Should().Be(20);
    }

    [Fact]
    public void PropertyChangeCommand_Undo_RestoresOldValue()
    {
        int currentValue = 10;
        var command = new PropertyChangeCommand<int>("TestProperty", v => currentValue = v, 10, 20);

        command.Execute();
        command.Undo();

        currentValue.Should().Be(10);
    }

    [Fact]
    public void PropertyChangeCommand_CanMergeWith_ReturnsTrueForSameProperty()
    {
        var command1 = new PropertyChangeCommand<int>("TestProperty", _ => { }, 10, 20);
        var command2 = new PropertyChangeCommand<int>("TestProperty", _ => { }, 20, 30);

        var canMerge = command1.CanMergeWith(command2);

        canMerge.Should().BeTrue();
    }

    [Fact]
    public void PropertyChangeCommand_CanMergeWith_ReturnsFalseForDifferentProperty()
    {
        var command1 = new PropertyChangeCommand<int>("Property1", _ => { }, 10, 20);
        var command2 = new PropertyChangeCommand<int>("Property2", _ => { }, 20, 30);

        var canMerge = command1.CanMergeWith(command2);

        canMerge.Should().BeFalse();
    }

    [Fact]
    public void PropertyChangeCommand_CanMergeWith_ReturnsFalseForNonContiguousValues()
    {
        var command1 = new PropertyChangeCommand<int>("TestProperty", _ => { }, 10, 20);
        var command2 = new PropertyChangeCommand<int>("TestProperty", _ => { }, 25, 30);

        var canMerge = command1.CanMergeWith(command2);

        canMerge.Should().BeFalse();
    }

    [Fact]
    public void PropertyChangeCommand_MergeWith_CreatesMergedCommand()
    {
        int currentValue = 10;
        Action<int> setter = v => currentValue = v;

        var command1 = new PropertyChangeCommand<int>("TestProperty", setter, 10, 20);
        var command2 = new PropertyChangeCommand<int>("TestProperty", setter, 20, 30);

        var merged = command1.MergeWith(command2) as PropertyChangeCommand<int>;

        merged.Should().NotBeNull();
        merged!.OldValue.Should().Be(10);
        merged.NewValue.Should().Be(30);
    }

    [Fact]
    public void PropertyChangeCommand_MergedCommand_UndoesCorrectly()
    {
        int currentValue = 10;
        Action<int> setter = v => currentValue = v;

        var command1 = new PropertyChangeCommand<int>("TestProperty", setter, 10, 20);
        var command2 = new PropertyChangeCommand<int>("TestProperty", setter, 20, 30);

        var merged = command1.MergeWith(command2) as PropertyChangeCommand<int>;
        merged!.Execute();
        currentValue.Should().Be(30);

        merged.Undo();
        currentValue.Should().Be(10);
    }

    [Fact]
    public void PropertyChangeCommand_MergeWith_StringValues()
    {
        string currentValue = "Hello";
        Action<string> setter = v => currentValue = v;

        var command1 = new PropertyChangeCommand<string>("Name", setter, "Hello", "Hello W");
        var command2 = new PropertyChangeCommand<string>("Name", setter, "Hello W", "Hello World");

        var merged = command1.MergeWith(command2) as PropertyChangeCommand<string>;

        merged.Should().NotBeNull();
        merged!.OldValue.Should().Be("Hello");
        merged.NewValue.Should().Be("Hello World");
    }

    #endregion

    #region DelegateCommand Tests

    [Fact]
    public void DelegateCommand_Constructor_SetsDescription()
    {
        var command = new DelegateCommand("Test", () => { }, () => { });

        command.Description.Should().Be("Test");
    }

    [Fact]
    public void DelegateCommand_Execute_CallsExecuteAction()
    {
        bool executed = false;
        var command = new DelegateCommand("Test", () => executed = true, () => { });

        command.Execute();

        executed.Should().BeTrue();
    }

    [Fact]
    public void DelegateCommand_Undo_CallsUndoAction()
    {
        bool undone = false;
        var command = new DelegateCommand("Test", () => { }, () => undone = true);

        command.Undo();

        undone.Should().BeTrue();
    }

    [Fact]
    public void DelegateCommand_Constructor_ThrowsOnNullDescription()
    {
        var action = () => new DelegateCommand(null!, () => { }, () => { });

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DelegateCommand_Constructor_ThrowsOnNullExecute()
    {
        var action = () => new DelegateCommand("Test", null!, () => { });

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DelegateCommand_Constructor_ThrowsOnNullUndo()
    {
        var action = () => new DelegateCommand("Test", () => { }, null!);

        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UndoBatch Tests

    [Fact]
    public void UndoBatch_Execute_AddsCommandToBatch()
    {
        var manager = new UndoManager();

        using (var batch = manager.BeginBatch("Test Batch"))
        {
            batch.Execute(CreateMockCommand("Command 1"));
            batch.Execute(CreateMockCommand("Command 2"));
        }

        manager.UndoCount.Should().Be(1);
        manager.NextUndoDescription.Should().Be("Test Batch");
    }

    [Fact]
    public void UndoBatch_Dispose_CommitsBatch()
    {
        var manager = new UndoManager();

        using (var batch = manager.BeginBatch("Test Batch"))
        {
            batch.Execute(CreateMockCommand("Command 1"));
        }

        manager.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void UndoBatch_EmptyBatch_DoesNotAddToHistory()
    {
        var manager = new UndoManager();

        using (var batch = manager.BeginBatch("Empty Batch"))
        {
            // No commands added
        }

        manager.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void UndoBatch_Cancel_UndoesAllCommands()
    {
        var manager = new UndoManager();
        int value = 0;

        using (var batch = manager.BeginBatch("Test Batch"))
        {
            batch.Execute(new DelegateCommand("Inc", () => value++, () => value--));
            batch.Execute(new DelegateCommand("Inc", () => value++, () => value--));
            value.Should().Be(2);

            batch.Cancel();
        }

        value.Should().Be(0);
        manager.CanUndo.Should().BeFalse();
    }

    #endregion

    #region AddItemCommand Tests

    [Fact]
    public void AddItemCommand_Execute_AddsItemToCollection()
    {
        var collection = new List<string>();
        var command = new AddItemCommand<string>("Add Item", collection, "Test");

        command.Execute();

        collection.Should().Contain("Test");
    }

    [Fact]
    public void AddItemCommand_Execute_InsertsAtSpecificIndex()
    {
        var collection = new List<string> { "A", "C" };
        var command = new AddItemCommand<string>("Add Item", collection, "B", 1);

        command.Execute();

        collection.Should().ContainInOrder("A", "B", "C");
    }

    [Fact]
    public void AddItemCommand_Undo_RemovesItem()
    {
        var collection = new List<string>();
        var command = new AddItemCommand<string>("Add Item", collection, "Test");

        command.Execute();
        command.Undo();

        collection.Should().BeEmpty();
    }

    #endregion

    #region RemoveItemCommand Tests

    [Fact]
    public void RemoveItemCommand_Execute_RemovesItemFromCollection()
    {
        var collection = new List<string> { "Test" };
        var command = new RemoveItemCommand<string>("Remove Item", collection, "Test");

        command.Execute();

        collection.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItemCommand_Undo_RestoresItemAtOriginalIndex()
    {
        var collection = new List<string> { "A", "B", "C" };
        var command = new RemoveItemCommand<string>("Remove Item", collection, "B");

        command.Execute();
        command.Undo();

        collection.Should().ContainInOrder("A", "B", "C");
    }

    #endregion

    #region MoveItemCommand Tests

    [Fact]
    public void MoveItemCommand_Execute_MovesItem()
    {
        var collection = new List<string> { "A", "B", "C" };
        var command = new MoveItemCommand<string>("Move Item", collection, 0, 2);

        command.Execute();

        collection.Should().ContainInOrder("B", "C", "A");
    }

    [Fact]
    public void MoveItemCommand_Undo_RestoresOriginalOrder()
    {
        var collection = new List<string> { "A", "B", "C" };
        var command = new MoveItemCommand<string>("Move Item", collection, 0, 2);

        command.Execute();
        command.Undo();

        collection.Should().ContainInOrder("A", "B", "C");
    }

    #endregion

    #region Helper Methods

    private static IUndoableCommand CreateMockCommand(string description)
    {
        var mock = new Mock<IUndoableCommand>();
        mock.Setup(c => c.Description).Returns(description);
        return mock.Object;
    }

    #endregion
}
