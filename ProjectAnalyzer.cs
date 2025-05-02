using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

public class ProjectAnalyzer
{
    public class ClassNode
    {
        public string Name { get; set; }
        public string BaseType { get; set; }
        public List<string> Interfaces { get; set; } = new List<string>();
        public List<string> ReferencedTypes { get; set; } = new List<string>();
        public List<MethodNode> Methods { get; set; } = new List<MethodNode>();
        public List<PropertyNode> Properties { get; set; } = new List<PropertyNode>();
    }

    public class MethodNode
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public List<ParameterNode> Parameters { get; set; } = new List<ParameterNode>();
    }

    public class ParameterNode
    {
        public string Type { get; set; }
        public string Name { get; set; }
    }

    public class PropertyNode
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Accessibility { get; set; }
        public bool IsStatic { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }
    }
    public async Task<List<ClassNode>> AnalyzeProject(string projectFilePath, ProgressReporter reporter)
    {
        var workspace = MSBuildWorkspace.Create();
        if (!File.Exists(projectFilePath))
            throw new FileNotFoundException("Project file not found");

        await reporter.ReportAsync(10, "Opening project...");
        var project = await workspace.OpenProjectAsync(projectFilePath);
        
        await reporter.ReportAsync(50, "Compiling project...");
        var compilation = await project.GetCompilationAsync();

        List<ClassNode> classGraph = new List<ClassNode>();

        var totalDocuments = project.Documents.Count();
        var processedDocuments = 0;
        int progress = 50;

        foreach (var document in project.Documents)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            var root = syntaxTree.GetRoot();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
                if (classSymbol == null) continue;

                var classNode = new ClassNode
                {
                    Name = classSymbol.Name,
                    BaseType = classSymbol.BaseType?.ToDisplayString() ?? "System.Object"
                };

                // Add interfaces
                classNode.Interfaces.AddRange(classSymbol.Interfaces.Select(i => i.ToDisplayString()));

                // Collect referenced types
                var typeCollector = new TypeCollector(semanticModel);
                classDecl.Accept(typeCollector);
                classNode.ReferencedTypes.AddRange(typeCollector.GetTypes().Select(t => t.ToDisplayString()));

                // Add methods
                foreach (var member in classSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method)
                    {
                        var methodNode = new MethodNode
                        {
                            Name = method.Name,
                            ReturnType = method.ReturnType.ToDisplayString()
                        };

                        methodNode.Parameters.AddRange(method.Parameters.Select(p =>
                            new ParameterNode
                            {
                                Type = p.Type.ToDisplayString(),
                                Name = p.Name
                            }
                        ));

                        classNode.Methods.Add(methodNode);
                    }
                    else if (member is IPropertySymbol property)
                    {
                        var propNode = new PropertyNode
                        {
                            Name = property.Name,
                            Type = property.Type.ToDisplayString(),
                            Accessibility = property.DeclaredAccessibility.ToString(),
                            IsStatic = property.IsStatic,
                            IsAbstract = property.IsAbstract,
                            IsVirtual = property.IsVirtual
                        };

                        classNode.Properties.Add(propNode);
                    }
                }

                classGraph.Add(classNode);
            }
        
            progress = 50 + (int)(50 * processedDocuments/totalDocuments);
            await reporter.ReportAsync(progress, $"Analyzing document: {document.Name}");
        }

        await reporter.ReportAsync(100, "Analysis complete.");
        return classGraph;
    }
}

public class TypeCollector : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly List<ITypeSymbol> _types = new List<ITypeSymbol>();

    public TypeCollector(SemanticModel semanticModel) : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
    }

    public override void Visit(SyntaxNode node)
    {
        if (node is TypeSyntax typeSyntax)
        {
            var typeInfo = _semanticModel.GetTypeInfo(typeSyntax);
            if (typeInfo.Type != null)
            {
                _types.Add(typeInfo.Type);
            }
        }
        base.Visit(node);
    }

    public List<ITypeSymbol> GetTypes()
    {
        return _types;
    }
}