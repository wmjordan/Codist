using System;
using System.Linq;
using System.Windows.Controls;
using CLR;
using Microsoft.CodeAnalysis;

namespace Codist.Controls
{
	sealed class ExtIconProvider
	{
		ExtIconProvider(bool containerIsInterface) {
			_ContainerIsInterface = containerIsInterface;
		}
		public static readonly ExtIconProvider Default = new ExtIconProvider(false);
		public static readonly ExtIconProvider InterfaceMembers = new ExtIconProvider(true);
		readonly bool _ContainerIsInterface;

		public StackPanel GetExtIcons(SymbolItem symbolItem) {
			return GetSpecialSymbolIcon(symbolItem.Symbol);
		}

		public StackPanel GetExtIconsWithUsage(SymbolItem symbolItem) {
			var icons = GetSpecialSymbolIcon(symbolItem.Symbol);
			if (symbolItem.Usage != SymbolUsageKind.Normal) {
				AddSymbolUsageIcons(ref icons, symbolItem.Usage);
			}
			return icons;
		}

		StackPanel GetSpecialSymbolIcon(ISymbol symbol) {
			StackPanel icons = null;
			switch (symbol.Kind) {
				case SymbolKind.Method:
					var ms = symbol as IMethodSymbol;
					if (ms.IsAsync || ms.ReturnType.IsAwaitable()) {
						AddIcon(ref icons, IconIds.AsyncMember);
					}
					if (ms.IsGenericMethod) {
						AddIcon(ref icons, IconIds.Generic);
					}
					if (ms.IsExtensionMethod) {
						AddIcon(ref icons, IconIds.ExtensionMethod);
					}
					else {
						if (ms.IsSealed) {
							AddIcon(ref icons, IconIds.SealedMethod);
						}
						else if (ms.IsVirtual) {
							AddIcon(ref icons, IconIds.VirtualMember);
						}
						if (ms.IsOverride) {
							AddIcon(ref icons, IconIds.OverrideMethod);
						}
						if (ms.IsReadOnly()) {
							AddIcon(ref icons, IconIds.ReadonlyMethod);
						}
						if (_ContainerIsInterface) {
							if (ms.IsAbstract == false) {
								if (ms.IsStatic == false) {
									AddIcon(ref icons, IconIds.DefaultInterfaceImplementation);
								}
							}
							else if (ms.IsStatic) {
								AddIcon(ref icons, IconIds.AbstractMember);
							}
						}
						else if (ms.IsAbstract) {
							AddIcon(ref icons, IconIds.AbstractMember);
						}
					}
					if (ms.ReturnsByRef || ms.ReturnsByRefReadonly) {
						AddIcon(ref icons, IconIds.RefMember);
					}
					break;
				case SymbolKind.NamedType:
					var type = symbol as INamedTypeSymbol;
					if (type.IsGenericType) {
						AddIcon(ref icons, IconIds.Generic);
					}
					switch (type.TypeKind) {
						case TypeKind.Class:
							if (type.IsSealed && type.IsStatic == false) {
								AddIcon(ref icons, IconIds.SealedClass);
							}
							else if (type.IsAbstract) {
								AddIcon(ref icons, IconIds.AbstractClass);
							}
							break;
						case TypeKind.Enum:
							if (type.GetAttributes().Any(a => a.AttributeClass.MatchTypeName(nameof(FlagsAttribute), "System"))) {
								AddIcon(ref icons, IconIds.EnumFlags);
							}
							break;
						case TypeKind.Struct:
							if (type.IsReadOnly()) {
								AddIcon(ref icons, IconIds.ReadonlyType);
							}
							if (type.IsRefLike()) {
								AddIcon(ref icons, IconIds.RefMember);
							}
							break;
					}
					break;
				case SymbolKind.Field:
					var f = symbol as IFieldSymbol;
					if (f.IsConst) {
						return null;
					}
					if (f.IsReadOnly) {
						AddIcon(ref icons, IconIds.ReadonlyField);
					}
					else if (f.IsVolatile) {
						AddIcon(ref icons, IconIds.VolatileField);
					}
					break;
				case SymbolKind.Event:
					if (_ContainerIsInterface == false) {
						if (symbol.IsAbstract) {
							AddIcon(ref icons, IconIds.AbstractMember);
						}
						else {
							if (symbol.IsSealed) {
								AddIcon(ref icons, IconIds.SealedEvent);
							}
							else if (symbol.IsVirtual) {
								AddIcon(ref icons, IconIds.VirtualMember);
							}
							if (symbol.IsOverride) {
								AddIcon(ref icons, IconIds.OverrideEvent);
							}
						}
					}
					break;
				case SymbolKind.Property:
					if (symbol is IPropertySymbol p) {
						if (_ContainerIsInterface == false) {
							if ((ms = p.SetMethod) == null) {
								AddIcon(ref icons, IconIds.ReadonlyProperty);
							}
							else if (ms.IsInitOnly()) {
								AddIcon(ref icons, IconIds.InitonlyProperty);
							}
							if ((ms = p.GetMethod) != null && ms.IsReadOnly()) {
								AddIcon(ref icons, IconIds.ReadonlyMethod);
							}
							if (symbol.IsAbstract) {
								AddIcon(ref icons, IconIds.AbstractMember);
							}
							else {
								if (symbol.IsSealed) {
									AddIcon(ref icons, IconIds.SealedProperty);
								}
								else if (symbol.IsVirtual) {
									AddIcon(ref icons, IconIds.VirtualMember);
								}
								if (symbol.IsOverride) {
									AddIcon(ref icons, IconIds.OverrideProperty);
								}
							}
						}
						else {
							if (p.IsAbstract && p.IsStatic) {
								AddIcon(ref icons, IconIds.AbstractMember);
							}
						}
						if (p.IsRequired()) {
							AddIcon(ref icons, IconIds.RequiredMember);
						}
						if (p.ReturnsByRef || p.ReturnsByRefReadonly) {
							AddIcon(ref icons, IconIds.RefMember);
						}
					}
					break;
				case SymbolKind.Namespace:
					return null;
			}
			if (symbol.IsStatic) {
				AddIcon(ref icons, IconIds.StaticMember);
			}
			return icons;
		}

		static void AddSymbolUsageIcons(ref StackPanel icons, SymbolUsageKind usage) {
			if (usage.MatchFlags(SymbolUsageKind.Write)) {
				AddIcon(ref icons, IconIds.UseToWrite);
				if (usage.MatchFlags(SymbolUsageKind.SetNull)) {
					AddIcon(ref icons, IconIds.UseToWriteNull);
				}
			}
			else if (usage.MatchFlags(SymbolUsageKind.Catch)) {
				AddIcon(ref icons, IconIds.UseToCatch);
			}
			else if (usage.HasAnyFlag(SymbolUsageKind.Attach | SymbolUsageKind.Detach | SymbolUsageKind.Trigger)) {
				if (usage.MatchFlags(SymbolUsageKind.Attach)) {
					AddIcon(ref icons, IconIds.AttachEvent);
				}
				if (usage.MatchFlags(SymbolUsageKind.Detach)) {
					AddIcon(ref icons, IconIds.DetachEvent);
				}
				if (usage.MatchFlags(SymbolUsageKind.Trigger)) {
					AddIcon(ref icons, IconIds.TriggerEvent);
				}
			}
			else if (usage.MatchFlags(SymbolUsageKind.TypeCast)) {
				AddIcon(ref icons, IconIds.UseToCast);
			}
			else if (usage.MatchFlags(SymbolUsageKind.TypeParameter)) {
				AddIcon(ref icons, IconIds.UseAsTypeParameter);
			}
			else if (usage.MatchFlags(SymbolUsageKind.Delegate)) {
				AddIcon(ref icons, IconIds.UseAsDelegate);
			}
		}

		static void AddIcon(ref StackPanel container, int imageId) {
			if (container == null) {
				container = new StackPanel { Orientation = Orientation.Horizontal };
			}
			container.Children.Add(VsImageHelper.GetImage(imageId));
		}
	}
}
