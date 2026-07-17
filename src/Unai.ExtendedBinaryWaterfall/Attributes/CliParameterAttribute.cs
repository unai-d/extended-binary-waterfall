using System;
using System.Reflection;

namespace Unai.ExtendedBinaryWaterfall.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class CliParameterAttribute : Attribute
{
	public string LongParameterName { get; set; } = null;
	public char? ShortParameterName { get; set; } = null;
	public string Name { get; set; } = null;
	public string Description { get; set; } = null;

	public CliParameterAttribute(string name, string longParamName, char shortParamName, string desc = null)
	{
		Name = name;
		LongParameterName = longParamName;
		ShortParameterName = shortParamName;
		Description = desc;
	}

	public CliParameterAttribute(string name, string longParamName, string desc = null)
	{
		Name = name;
		LongParameterName = longParamName;
		Description = desc;
	}

	public CliParameterAttribute() {}

	public static bool SetPropertyFromCliArgument(PropertyInfo targetProp, object targetObject, string value)
	{
		if (targetProp.PropertyType == typeof(string))
		{
			targetProp.SetValue(targetObject, value.Replace("\\n", "\n"));
		}
		else if (targetProp.PropertyType == typeof(int))
		{
			targetProp.SetValue(targetObject, int.Parse(value));
		}
		else if (targetProp.PropertyType == typeof(long))
		{
			targetProp.SetValue(targetObject, long.Parse(value));
		}
		else if (targetProp.PropertyType.IsEnum)
		{
			var ok = Enum.TryParse(targetProp.PropertyType, value, true, out var pval);
			if (!ok)
			{
				Logger.Error($"Cannot parse value '{value}' to enumeration '{targetProp.PropertyType.Name}'.");
				Logger.Info("Valid values:");
				foreach (var enumVal in Enum.GetValues(targetProp.PropertyType))
				{
					Logger.Info($"	{enumVal}");
				}
				return false;
			}
			targetProp.SetValue(targetObject, pval);
		}
		else if (targetProp.PropertyType == typeof(bool))
		{
			targetProp.SetValue(targetObject, bool.Parse(value));
		}
		else
		{
			Logger.Error($"Cannot convert string representation of value of property `{targetProp.Name}` to {targetProp.PropertyType} because it is not implemented yet.");
		}
		return true;
	}
}
