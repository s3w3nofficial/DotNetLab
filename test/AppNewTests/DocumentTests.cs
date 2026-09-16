using AwesomeAssertions;
using DotNetLab.Features.Documents;
using DotNetLab.Lab;
using Fluxor;

namespace DotNetLab;

[TestClass]
public sealed class DocumentTests
{
    [TestMethod]
    public void DisplayName_SpecialSources()
    {
        LabDocuments.IsSpecialSource("Directives.cs").Should().BeTrue();
        LabDocuments.IsSpecialSource("Configuration.cs").Should().BeTrue();
        LabDocuments.IsSpecialSource("Program.cs").Should().BeFalse();
        LabDocuments.DisplayName("Directives.cs").Should().Be("Directives");
        LabDocuments.DisplayName("Configuration.cs").Should().Be("Configuration");
        LabDocuments.DisplayName("Program.cs").Should().Be("Program.cs");
    }

    [TestMethod]
    public void StartsWithCSharpProgram()
    {
        var (documents, host, _) = Create();
        documents.Template.Should().Be("C#");
        documents.ActiveSource.Should().Be("Program.cs");
        documents.SourceFiles.Should().Equal("Program.cs");
        documents.Sources["Program.cs"].Should().Be(InitialCode.CSharp.TextTemplate);
        host.ActiveOutput.Should().Be("cs");
    }

    [TestMethod]
    public async Task SetTemplate_RazorReplacesUserFiles()
    {
        var (documents, host, dispatcher) = Create();
        documents.SetTemplate("Razor");
        await host.LastAfterDocumentsChanged!;

        documents.Template.Should().Be("Razor");
        documents.ActiveSource.Should().Be("TestComponent.razor");
        documents.SourceFiles.Should().Equal("TestComponent.razor", "_Imports.razor");
        host.ActiveOutput.Should().Be("gcs");
        host.Stale.Should().BeTrue();
        host.PersistCount.Should().Be(1);
        dispatcher.Actions.Should().ContainItemsAssignableTo<SetDocumentsAction>();
    }

    [TestMethod]
    public void SetTemplate_Cshtml()
    {
        var (documents, host, _) = Create();
        documents.SetTemplate("CSHTML");
        documents.ActiveSource.Should().Be("TestPage.cshtml");
        documents.SourceFiles.Should().Equal("TestPage.cshtml");
        host.ActiveOutput.Should().Be("gcs");
    }

    [TestMethod]
    public void AddAndCloseFile()
    {
        var (documents, host, _) = Create();
        documents.AddFile(".cs");
        documents.ActiveSource.Should().Be("File1.cs");
        documents.SourceFiles.Should().Equal("Program.cs", "File1.cs");
        host.PersistCount.Should().Be(1);

        documents.CloseFile("Program.cs");
        documents.ActiveSource.Should().Be("File1.cs");
        documents.SourceFiles.Should().Equal("File1.cs");

        documents.CloseFile("File1.cs");
        documents.SourceFiles.Should().Equal("File1.cs");
    }

    [TestMethod]
    public void RenameFile_AcceptsAndRejects()
    {
        var (documents, host, _) = Create();
        documents.RenameFile("Program.cs", "Hello.cs");
        documents.ActiveSource.Should().Be("Hello.cs");
        documents.SourceFiles.Should().Equal("Hello.cs");
        host.PersistCount.Should().Be(1);

        documents.RenameFile("Hello.cs", "..");
        documents.SourceFiles.Should().Equal("Hello.cs");

        documents.OpenDirectives();
        documents.SourceFiles.Should().Equal("Hello.cs", "Directives.cs");
        documents.RenameFile("Directives.cs", "Other.cs");
        documents.SourceFiles.Should().Equal("Hello.cs", "Directives.cs");
    }

    [TestMethod]
    public void SetSource_MarksStaleOnce()
    {
        var (documents, host, _) = Create();
        host.Stale = false;
        documents.SetSource("Program.cs", "class C;");
        host.Stale.Should().BeTrue();
        host.StatusCount.Should().Be(1);

        documents.SetSource("Program.cs", "class D;");
        host.StatusCount.Should().Be(1);
        documents.Sources["Program.cs"].Should().Be("class D;");
    }

    [TestMethod]
    public void LoadFromSavedState_InfersRazorTemplate()
    {
        var (documents, _, _) = Create();
        documents.LoadFromSavedState(SavedState.Razor);
        documents.Template.Should().Be("Razor");
        documents.ActiveSource.Should().Be("TestComponent.razor");
        documents.SourceFiles.Should().Equal("TestComponent.razor", "_Imports.razor");
    }

    [TestMethod]
    public void LoadImportedFiles_ReplacesUserFiles()
    {
        var (documents, host, _) = Create();
        documents.LoadImportedFiles(new Dictionary<string, string>
        {
            ["sub/App.cs"] = "class App;",
            [".."] = "ignored",
        });
        documents.SourceFiles.Should().Equal("App.cs");
        documents.Sources["App.cs"].Should().Be("class App;");
        documents.ActiveSource.Should().Be("App.cs");
        host.Stale.Should().BeTrue();
        host.PersistCount.Should().Be(1);
    }

    private static (LabDocuments Documents, FakeDocumentWorkspace Host, FakeDispatcher Dispatcher) Create()
    {
        var host = new FakeDocumentWorkspace();
        var dispatcher = new FakeDispatcher();
        return (new LabDocuments(host, dispatcher), host, dispatcher);
    }

    private sealed class FakeDocumentWorkspace : IDocumentWorkspace
    {
        public string ActiveOutput { get; set; } = "cs";
        public bool Stale { get; set; }
        public int PersistCount { get; private set; }
        public int StatusCount { get; private set; }
        public Task? LastAfterDocumentsChanged { get; private set; }

        public void EnsureActiveOutput()
        {
        }

        public void Notify()
        {
        }

        public void NotifyStatus() => StatusCount++;

        public Task AfterDocumentsChangedAsync(IReadOnlyList<string> before)
        {
            LastAfterDocumentsChanged = Task.CompletedTask;
            _ = before;
            return LastAfterDocumentsChanged;
        }

        public Task PersistUrlAsync(bool snapshot = false)
        {
            PersistCount++;
            return Task.CompletedTask;
        }

        public void AfterActiveSourceChanged()
        {
            PersistCount++;
        }
    }

    private sealed class FakeDispatcher : IDispatcher
    {
        public List<object> Actions { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched
        {
            add { }
            remove { }
        }

        public void Dispatch(object action) => Actions.Add(action);
    }
}
