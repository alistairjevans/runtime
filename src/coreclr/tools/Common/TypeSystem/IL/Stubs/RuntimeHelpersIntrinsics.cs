// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Provides method bodies for System.Runtime.CompilerServices.RuntimeHelpers intrinsics.
    /// </summary>
    public static class RuntimeHelpersIntrinsics
    {
        public static MethodIL EmitIL(MethodDesc method)
        {
            Debug.Assert(((MetadataType)method.OwningType).Name == "RuntimeHelpers");
            string methodName = method.Name;

            // All the methods handled below are per-instantiation generic methods
            if (method.Instantiation.Length != 1 || method.IsTypicalMethodDefinition)
                return null;

            TypeDesc elementType = method.Instantiation[0];

            // Fallback to non-intrinsic implementation for universal generics
            if (elementType.IsCanonicalSubtype(CanonicalFormKind.Universal))
                return null;

            bool result;
            if (methodName == "IsBitwiseEquatable")
            {
                // Ideally we could detect automatically whether a type is trivially equatable
                // (i.e., its operator == could be implemented via memcmp). But for now we'll
                // do the simple thing and hardcode the list of types we know fulfill this contract.
                // n.b. This doesn't imply that the type's CompareTo method can be memcmp-implemented,
                // as a method like CompareTo may need to take a type's signedness into account.
                switch (elementType.UnderlyingType.Category)
                {
                    case TypeFlags.Boolean:
                    case TypeFlags.Byte:
                    case TypeFlags.SByte:
                    case TypeFlags.Char:
                    case TypeFlags.UInt16:
                    case TypeFlags.Int16:
                    case TypeFlags.UInt32:
                    case TypeFlags.Int32:
                    case TypeFlags.UInt64:
                    case TypeFlags.Int64:
                    case TypeFlags.IntPtr:
                    case TypeFlags.UIntPtr:
                        result = true;
                        break;
                    default:
                        result = false;
                        if (elementType is MetadataType mdType)
                        {
                            if (mdType.Module == mdType.Context.SystemModule &&
                                mdType.Namespace == "System.Text" &&
                                mdType.Name == "Rune")
                            {
                                result = true;
                            }
                            else if (mdType.IsValueType)
                            {
                                bool? equatable = ComparerIntrinsics.ImplementsIEquatable(mdType.GetTypeDefinition());

                                if (equatable.HasValue && !equatable.Value)
                                {
                                    // Value type that can use memcmp and that doesn't override object.Equals or implement IEquatable<T>.Equals.
                                    MethodDesc objectEquals = mdType.Context.GetWellKnownType(WellKnownType.Object).GetMethod("Equals", null);
                                    result =
                                        mdType.FindVirtualFunctionTargetMethodOnObjectType(objectEquals).OwningType != mdType &&
                                        ComparerIntrinsics.CanCompareValueTypeBits(mdType, objectEquals);
                                }
                            }
                        }
                        break;
                }
            }
            else
            {
                return null;
            }

            ILOpcode opcode = result ? ILOpcode.ldc_i4_1 : ILOpcode.ldc_i4_0;

            return new ILStubMethodIL(method, new byte[] { (byte)opcode, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), Array.Empty<object>());
        }
    }
}
