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
    public async Task<List<ClassNode>> AnalyzeProject(string projectDirectory)
    {
        var workspace = MSBuildWorkspace.Create();
        var projectFiles = Directory.GetFiles(projectDirectory, "*.csproj");

        if (projectFiles.Length == 0)
            throw new FileNotFoundException("No .csproj file found");

        var project = await workspace.OpenProjectAsync(projectFiles[0]);
        var compilation = await project.GetCompilationAsync();

        List<ClassNode> classGraph = new List<ClassNode>();

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
        }

        return classGraph;
    }
}

public class TypeCollector : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly List<ITypeSymbol> _types = new List<ITypeSymbol>();

    public TypeCollector(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public void VisitTypeReference(TypeSyntax node)
    {
        var typeInfo = _semanticModel.GetTypeInfo(node);
        if (typeInfo.Type != null)
        {
            _types.Add(typeInfo.Type);
        }
    }

    public List<ITypeSymbol> GetTypes()
    {
        return _types;
    }
}