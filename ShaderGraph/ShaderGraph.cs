using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SharpDX;

namespace ShaderGraphExperiment
{
    public interface IShaderNode
    {
        bool HasChanged { get; }
    }

    public interface IShaderNode<T> : IShaderNode
    {
    }

    public interface IMadeUpShaderNode : IShaderNode
    {
        string Phrase { get; }
    }

    public class MadeUpShaderNode : IMadeUpShaderNode
    {
        public string Phrase { get; private set; }
        public bool HasChanged => false;

        public MadeUpShaderNode(string phrase)
        {
            Phrase = phrase;
        }
    }

    public interface IFunctionNode : IShaderNode
    {
        string Name { get; }
        string Code { get; }
        VarDeclaration CreateShaderCallAssignment(string localVar, IEnumerable<string> arguments);
        IEnumerable<IShaderNode> NodeArguments { get; }
    }

    public interface IFunctionNode<T> : IFunctionNode, IShaderNode<T>
    {
    }

    public interface IGlobalVarNode : IShaderNode
    {
        VarDeclaration GetGlobalVar(string[] reserved);
    }

    public interface IGlobalVarNode<T> : IGlobalVarNode, IShaderNode<T>
    {
    }

    public struct VarDeclaration
    {
        public VarDeclaration(string type, string identifier, string rightHandSide)
        {
            Type = type;
            Identifier = identifier;
            RightHandSide = rightHandSide;
        }

        public string Type;
        public string Identifier;
        public string RightHandSide;

        public override string ToString() => $"{Type} {Identifier} = {RightHandSide};";
    }

    public class Default<T> : IGlobalVarNode<T>
    {
        string Name;
        T Value;

        public Default(string name)
        {
            Name = name;
            Value = default(T);
        }
        public Default(string name, T value)
        {
            Name = name;
            Value = value;
        }

        public bool HasChanged => false;

        public VarDeclaration GetGlobalVar(string[] reserved)
            => new VarDeclaration(ShaderGraph.GetTypeName<T>(), ShaderGraph.GetUniqueName(Name, reserved), Value.GetHLSLValue());
    }

    public class ShaderTraverseResult
    {
        public Dictionary<string, string> FunctionDeclarations = new Dictionary<string, string>();
        public Dictionary<IGlobalVarNode, VarDeclaration> GlobalVars = new Dictionary<IGlobalVarNode, VarDeclaration>();
        public Dictionary<IFunctionNode, VarDeclaration> LocalDeclarations = new Dictionary<IFunctionNode, VarDeclaration>();

        public string GetPhrase(IFunctionNode node) => LocalDeclarations[node].Identifier;
        public string GetPhrase(IMadeUpShaderNode node) => node.Phrase;
        public string GetPhrase(IGlobalVarNode node) => GlobalVars[node].Identifier;
        public string GetPhrase(IShaderNode node) => GetPhrase((dynamic)node);
    }

    public static class ShaderGraph
    {
        public const string Category = "DX11.ShaderGraph";
        public const string DistanceField2DVersion = "DistanceField2D";

        public static ShaderTraverseResult Traverse(this IShaderNode node, ShaderTraverseResult result = null)
        {
            if (result == null)
                result = new ShaderTraverseResult();

            if (node != null)
                return Traverse((dynamic)node, result);

            return result;
        }

        public static ShaderTraverseResult Traverse(this IMadeUpShaderNode node, ShaderTraverseResult result = null) => result;

        static ShaderTraverseResult Traverse(this IGlobalVarNode node, ShaderTraverseResult result = null)
        {
            if (result.GlobalVars.ContainsKey(node))
                return result;

            result.GlobalVars[node] = node.GetGlobalVar(result.GlobalVars.Values.Select(gv => gv.Identifier).ToArray());
            return result;
        }

        static ShaderTraverseResult Traverse(this IFunctionNode node, ShaderTraverseResult result = null)
        {
            if (result.LocalDeclarations.ContainsKey(node))
                return result;

            foreach (var arg in node.NodeArguments)
                arg.Traverse(result);

            result.FunctionDeclarations[node.Name] = node.Code;

            var args = node.NodeArguments.Select(a => result.GetPhrase(a));

            result.LocalDeclarations[node] = node.CreateShaderCallAssignment($"var{result.LocalDeclarations.Count}", args);

            return result;
        }

        public static string GetUniqueName(this string preferredName, string[] reserved)
        {
            if (string.IsNullOrWhiteSpace(preferredName))
                preferredName = "Anonymous";

            if (reserved.Contains(preferredName))
            {
                int i = 2;
                while (reserved.Contains(preferredName + i)) i++;
                return preferredName + i;
            }
            return preferredName;
        }

        public static string GetTypeName<T>()
        {
            if (typeof(T) == typeof(float)) return "float";
            if (typeof(T) == typeof(Vector2)) return "float2";
            if (typeof(T) == typeof(Vector3)) return "float3";
            if (typeof(T) == typeof(Vector4)) return "float4";

            throw new NotImplementedException();
        }

        static string GetHLSLValue(float value) => value.ToString(CultureInfo.InvariantCulture.NumberFormat);
        static string GetHLSLValue(Vector2 value) => $"float2({GetHLSLValue(value.X)}, {GetHLSLValue(value.Y)})";
        static string GetHLSLValue(Vector3 value) => $"float3({GetHLSLValue(value.X)}, {GetHLSLValue(value.Y)}, {GetHLSLValue(value.Z)})";
        static string GetHLSLValue(Vector4 value) => $"float4({GetHLSLValue(value.X)}, {GetHLSLValue(value.Y)}, {GetHLSLValue(value.Z)}, {GetHLSLValue(value.W)})";
        static string GetHLSLValue(Color4 value) => $"float4({GetHLSLValue(value.Red)}, {GetHLSLValue(value.Green)}, {GetHLSLValue(value.Blue)}, {GetHLSLValue(value.Alpha)})";
        public static string GetHLSLValue<T>(this T value) => GetHLSLValue((dynamic)value);

    }
}