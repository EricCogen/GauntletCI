namespace GauntletCI.Core.Semantics;

/// <summary>
/// Generates counterfactual witnesses from patch operations and transformations.
/// 
/// A counterfactual is a small scenario (input or state) that would make old and new code behave differently.
/// This generator uses patch-visible signals to infer likely scenarios without full symbolic execution.
/// </summary>
public static class PatchCounterfactualGenerator
{
    /// <summary>
    /// Generate counterfactuals from a patch's operations and transformations.
    /// </summary>
    public static PatchCounterfactualCollection GenerateCounterfactuals(
        PatchOperationCollection operations,
        PatchTransformationCollection transformations,
        PatchModel patchModel)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(transformations);
        ArgumentNullException.ThrowIfNull(patchModel);

        var counterfactuals = new PatchCounterfactualCollection();

        // Generate from operations
        GenerateFromOperations(operations, patchModel, counterfactuals);

        // Generate from transformations
        GenerateFromTransformations(transformations, patchModel, counterfactuals);

        return counterfactuals;
    }

    private static void GenerateFromOperations(
        PatchOperationCollection operations,
        PatchModel patchModel,
        PatchCounterfactualCollection counterfactuals)
    {
        foreach (var op in operations.All)
        {
            switch (op.Kind)
            {
                case PatchOperationKind.ConditionalModified:
                    counterfactuals.Add(PatchCounterfactualFactory.BoundaryValue(
                        "Test boundary where condition changed",
                        op.FilePath,
                        op.NewLineNumber));
                    break;

                case PatchOperationKind.ConditionalRemoved:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Test case filtered by removed condition"));
                    break;

                case PatchOperationKind.FunctionBodyModified:
                    counterfactuals.Add(PatchCounterfactualFactory.ReturnValueCase("Function behavior may differ"));
                    break;

                case PatchOperationKind.FunctionRemoved:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Any call to removed function would fail"));
                    break;

                case PatchOperationKind.TypeChanged:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase($"Type changed from {op.Before} to {op.After}"));
                    break;

                case PatchOperationKind.ParameterRemoved:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Behavior may differ without this parameter"));
                    break;

                case PatchOperationKind.ParameterAdded:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Function calls must be updated or use default"));
                    break;

                case PatchOperationKind.ArgumentRemoved:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Function called with fewer arguments"));
                    break;

                case PatchOperationKind.LineRemoved:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Behavior depending on removed line is affected"));
                    break;

                case PatchOperationKind.IdentifierRenamed:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase($"Identifier renamed from {op.Before} to {op.After}"));
                    break;

                case PatchOperationKind.LineAdded:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("New behavior from added line"));
                    break;
            }
        }
    }

    private static void GenerateFromTransformations(
        PatchTransformationCollection transformations,
        PatchModel patchModel,
        PatchCounterfactualCollection counterfactuals)
    {
        foreach (var trans in transformations.All)
        {
            switch (trans.Kind)
            {
                case PatchTransformationKind.ExtractMethod:
                    counterfactuals.Add(PatchCounterfactualFactory.ReturnValueCase("Extracted method should be semantically equivalent"));
                    break;

                case PatchTransformationKind.InlineMethod:
                    counterfactuals.Add(PatchCounterfactualFactory.ReturnValueCase("Inlined implementation should preserve semantics"));
                    break;

                case PatchTransformationKind.LogicSimplified:
                    counterfactuals.Add(PatchCounterfactualFactory.BoundaryValue("Test edge cases after simplification"));
                    break;

                case PatchTransformationKind.ExceptionHandlingChanged:
                    counterfactuals.Add(PatchCounterfactualFactory.ExceptionCase("Test scenarios that trigger exceptions"));
                    break;

                case PatchTransformationKind.LoopModified:
                    counterfactuals.Add(PatchCounterfactualFactory.BoundaryValue("Test loop boundary cases"));
                    break;

                case PatchTransformationKind.DataStructureChanged:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Test values specific to new structure"));
                    break;

                case PatchTransformationKind.RenameMethod:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Callers must use new method name"));
                    break;

                case PatchTransformationKind.RenameClass:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("References must use new class name"));
                    break;

                case PatchTransformationKind.InheritanceChanged:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Inherited behavior may differ"));
                    break;

                case PatchTransformationKind.AccessModifierChanged:
                    counterfactuals.Add(PatchCounterfactualFactory.DefaultValueCase("Access restrictions have changed"));
                    break;
            }
        }
    }
}
