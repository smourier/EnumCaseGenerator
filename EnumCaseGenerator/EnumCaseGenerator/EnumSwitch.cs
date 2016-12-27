using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EnumCaseGenerator
{
    public class EnumSwitch
    {
        private List<IFieldSymbol> _existingFields = new List<IFieldSymbol>();
        private List<IFieldSymbol> _missingFields = new List<IFieldSymbol>();

        private EnumSwitch(SwitchStatementSyntax node, ITypeSymbol type)
        {
            Node = node;
            EnumType = type;
            _missingFields.AddRange(EnumType.GetMembers().OfType<IFieldSymbol>());

            for (int i = 0; i < node.Sections.Count; i++)
            {
                foreach (var label in node.Sections[i].Labels)
                {
                    if (label is DefaultSwitchLabelSyntax)
                    {
                        DefaultCase = (DefaultSwitchLabelSyntax)label;
                        DefaultCaseIsLast = i == node.Sections.Count - 1;
                        continue;
                    }

                    var caseLabel = label as CaseSwitchLabelSyntax;
                    if (caseLabel == null)
                        continue;

                    var member = caseLabel.Value as MemberAccessExpressionSyntax;
                    if (member == null)
                        continue;

                    var name = member.Name as IdentifierNameSyntax;
                    if (name == null || name.Identifier == null || string.IsNullOrWhiteSpace(name.Identifier.Text))
                        continue;

                    var field = _missingFields.FirstOrDefault(f => f.Name == name.Identifier.Text);
                    if (field == null)
                        continue;

                    _existingFields.Add(field);
                    _missingFields.Remove(field);
                }
            }

            var sorted = new List<IFieldSymbol>(_existingFields);
            sorted.Sort(new FieldComparer(true));
            IsSortedAlphabetically = IsSame(_existingFields, sorted);

            sorted.Sort(new FieldComparer(false));
            IsSortedByValues = IsSame(_existingFields, sorted);
        }

        public SwitchStatementSyntax Node { get; }
        public ITypeSymbol EnumType { get; }
        public bool IsSortedAlphabetically { get; }
        public bool IsSortedByValues { get; }
        public bool? DefaultCaseIsLast { get; }

        public DefaultSwitchLabelSyntax DefaultCase { get; }
        public bool HasDefaultCase => DefaultCase != null;
        public bool HasMissingCases => _missingFields.Count > 0;
        public IReadOnlyList<IFieldSymbol> ExistingFields => _existingFields;
        public IReadOnlyList<IFieldSymbol> MissingFields => _missingFields;

        public static EnumSwitch Parse(SemanticModel model, SwitchStatementSyntax node)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (node == null)
                throw new ArgumentNullException(nameof(node));

            // get type and check it's an enum
            var ti = model.GetTypeInfo(node.Expression);
            var type = ti.ConvertedType;
            if (type == null || type.TypeKind != TypeKind.Enum)
                return null;

            // is it a flags (multi-valued) attribute? we don't want to handle those
            var flags = type.GetAttributes().Any(a => a.AttributeClass?.ContainingNamespace?.Name == typeof(FlagsAttribute).Namespace && a.AttributeClass?.Name == typeof(FlagsAttribute).Name);
            if (flags)
                return null;

            return new EnumSwitch(node, type);
        }

        private static bool IsSame<T>(List<T> first, List<T> second)
        {
            if (first.Count != second.Count)
                return false;

            for (int i = 0; i < first.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(first[i], second[i]))
                    return false;
            }
            return true;
        }

        private class FieldComparer : IComparer<IFieldSymbol>
        {
            private bool _alpha;

            public FieldComparer(bool alpha)
            {
                _alpha = alpha;
            }

            public int Compare(IFieldSymbol x, IFieldSymbol y)
            {
                if (x != null && y != null)
                {
                    if (_alpha)
                    {
                        if (x.Name != null)
                            return x.Name.CompareTo(y.Name);
                    }
                    else
                    {
                        // enum value are comparable (hopefully)
                        var cx = x.ConstantValue as IComparable;
                        if (cx != null)
                        {
                            try
                            {
                                return cx.CompareTo(y.ConstantValue);
                            }
                            catch
                            {
                                // hmm... maybe this could happen if types are not compatible, so we do nothing
                            }
                        }
                    }
                }
                return 0; // don't know
            }
        }
    }
}
