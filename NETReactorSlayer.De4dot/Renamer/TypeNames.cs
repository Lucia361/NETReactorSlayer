using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace NETReactorSlayer.De4dot.Renamer
{
    public abstract class TypeNames
    {
        public string Create(TypeSig typeRef)
        {
            typeRef = typeRef.RemovePinnedAndModifiers();
            if (typeRef == null)
                return UnknownNameCreator.Create();
            if (typeRef is GenericInstSig gis)
                if (gis.FullName == "System.Nullable`1" &&
                    gis.GenericArguments.Count == 1 && gis.GenericArguments[0] != null)
                    typeRef = gis.GenericArguments[0];

            var prefix = GetPrefix(typeRef);

            var elementType = Renamer.GetScopeType(typeRef);
            if (elementType == null && IsFnPtrSig(typeRef))
                return FnPtrNameCreator.Create();
            if (IsGenericParam(elementType))
                return GenericParamNameCreator.Create();

            var typeFullName = typeRef.FullName;
            if (TypeNamesDict.TryGetValue(typeFullName, out var nc))
                return nc.Create();

            var fullName = elementType == null ? typeRef.FullName : elementType.FullName;
            var dict = prefix == "" ? FullNameToShortName : FullNameToShortNamePrefix;
            if (!dict.TryGetValue(fullName, out var shortName))
            {
                fullName = fullName.Replace('/', '.');
                var index = fullName.LastIndexOf('.');
                shortName = index > 0 ? fullName.Substring(index + 1) : fullName;

                index = shortName.LastIndexOf('`');
                if (index > 0)
                    shortName = shortName.Substring(0, index);
            }

            return AddTypeName(typeFullName, shortName, prefix).Create();
        }

        private static bool IsFnPtrSig(TypeSig sig)
        {
            while (sig != null)
            {
                if (sig is FnPtrSig)
                    return true;
                sig = sig.Next;
            }

            return false;
        }

        private static bool IsGenericParam(ITypeDefOrRef tdr)
        {
            var ts = tdr as TypeSpec;
            if (ts == null)
                return false;
            var sig = ts.TypeSig.RemovePinnedAndModifiers();
            return sig is GenericSig;
        }

        private static string GetPrefix(TypeSig typeRef)
        {
            var prefix = "";
            while (typeRef != null)
            {
                if (typeRef.IsPointer)
                    prefix += "p";
                typeRef = typeRef.Next;
            }

            return prefix;
        }

        protected INameCreator AddTypeName(string fullName, string newName, string prefix)
        {
            newName = FixName(prefix, newName);

            var name2 = " " + newName;
            if (!TypeNamesDict.TryGetValue(name2, out var nc))
                TypeNamesDict[name2] = nc = new NameCreator(newName + "_");

            TypeNamesDict[fullName] = nc;
            return nc;
        }

        protected abstract string FixName(string prefix, string name);

        public virtual TypeNames Merge(TypeNames other)
        {
            if (this == other)
                return this;
            foreach (var pair in other.TypeNamesDict)
                if (TypeNamesDict.TryGetValue(pair.Key, out var nc))
                    nc.Merge(pair.Value);
                else
                    TypeNamesDict[pair.Key] = pair.Value.Clone();
            GenericParamNameCreator.Merge(other.GenericParamNameCreator);
            FnPtrNameCreator.Merge(other.FnPtrNameCreator);
            UnknownNameCreator.Merge(other.UnknownNameCreator);
            return this;
        }

        protected static string UpperFirst(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Substring(0, 1).ToUpperInvariant() + s.Substring(1);
        }

        protected NameCreator FnPtrNameCreator = new NameCreator("fnptr_");
        protected Dictionary<string, string> FullNameToShortName;
        protected Dictionary<string, string> FullNameToShortNamePrefix;
        protected NameCreator GenericParamNameCreator = new NameCreator("gparam_");

        protected Dictionary<string, NameCreator> TypeNamesDict =
            new Dictionary<string, NameCreator>(StringComparer.Ordinal);

        protected NameCreator UnknownNameCreator = new NameCreator("unknown_");
    }
}