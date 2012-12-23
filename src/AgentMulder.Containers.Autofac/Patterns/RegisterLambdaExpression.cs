﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using AgentMulder.ReSharper.Domain.Patterns;
using AgentMulder.ReSharper.Domain.Registrations;
using AgentMulder.ReSharper.Domain.Utils;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Services.CSharp.StructuralSearch;
using JetBrains.ReSharper.Psi.Services.CSharp.StructuralSearch.Placeholders;
using JetBrains.ReSharper.Psi.Services.StructuralSearch;
using JetBrains.ReSharper.Psi.Tree;

namespace AgentMulder.Containers.Autofac.Patterns
{
    [Export("ComponentRegistration", typeof(IRegistrationPattern))]
    internal sealed class RegisterLambdaExpression : RegistrationPatternBase
    {
        private readonly IEnumerable<IBasedOnPattern> basedOnPatterns;

        private static readonly IStructuralSearchPattern pattern = 
            new CSharpStructuralSearchPattern("$builder$.Register($args$ => $expression$)",
                new ExpressionPlaceholder("builder", "global::Autofac.ContainerBuilder", true),
                new ArgumentPlaceholder("args", -1, -1),
                new ExpressionPlaceholder("expression"));

        [ImportingConstructor]
        public RegisterLambdaExpression([ImportMany] IEnumerable<IBasedOnPattern> basedOnPatterns)
            : base(pattern)
        {
            this.basedOnPatterns = basedOnPatterns;
        }

        public override IEnumerable<IComponentRegistration> GetComponentRegistrations(ITreeNode registrationRootElement)
        {
            var parentExpression = registrationRootElement.GetParentExpression<IExpressionStatement>();
            if (parentExpression == null)
            {
                yield break;
            }

            IStructuralMatchResult match = Match(registrationRootElement);

            if (match.Matched)
            {
                var expression = match.GetMatchedElement<ICSharpExpression>("expression");

                IEnumerable<IComponentRegistration> componentRegistrations = GetRegistrationsFromExpression(registrationRootElement, expression);

                IEnumerable<FilteredRegistrationBase> basedOnRegistrations = basedOnPatterns.SelectMany(
                   basedOnPattern => basedOnPattern.GetBasedOnRegistrations(parentExpression.Expression)).ToList();

                yield return new CompositeRegistration(registrationRootElement, componentRegistrations.Concat(basedOnRegistrations));
            }
        }

        private IEnumerable<IComponentRegistration> GetRegistrationsFromExpression(ITreeNode registrationRootElement, ICSharpExpression expression)
        {
            var castExpression = expression as ICastExpression;
            if (castExpression != null)
            {
                var typeUsage = castExpression.TargetType as IPredefinedTypeUsage;
                if (typeUsage != null && typeUsage.ScalarPredefinedTypeName != null)
                {
                    IResolveResult resolveResult = typeUsage.ScalarPredefinedTypeName.Reference.Resolve().Result;
                    var typeElement = resolveResult.DeclaredElement as ITypeElement;
                    if (typeElement != null)
                    {
                        yield return new ServiceRegistration(registrationRootElement, typeElement);
                    }
                }
            }

            var objectCreationExpression = expression as IObjectCreationExpression;
            if (objectCreationExpression != null && objectCreationExpression.TypeReference != null)
            {
                IResolveResult resolveResult = objectCreationExpression.TypeReference.Resolve().Result;
                var @class = resolveResult.DeclaredElement as IClass;
                if (@class != null)
                {
                    yield return new ComponentRegistration(registrationRootElement, @class, @class);
                }
            }
        }
    }
}