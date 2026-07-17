using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unai.ExtendedBinaryWaterfall.Attributes;
using Unai.ExtendedBinaryWaterfall.Exporters;
using Unai.ExtendedBinaryWaterfall.Parsers;

namespace Unai.ExtendedBinaryWaterfall.Cli;

class Program
{
	static bool _helpMode = false;

	static readonly Generator _generator = new();

	static void Main(string[] args)
	{
		// Make decimals use "." instead of other characters.
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

		Logger.Info($"{BuildInfo.ApplicationName} {BuildInfo.SemVer ?? "unknown"}");

		if (args.Length < 1)
		{
			Logger.Error("At least one argument must be specified.");
			PrintHelp();
			return;
		}

		if (!ParseCommandLineArguments(args))
		{
			Logger.Fail("Invalid command line arguments. Exiting…");
			return;
		}

		if (_helpMode)
		{
			PrintHelp();
			return;
		}

		try
		{
			_generator.Initialize();
			_generator.Generate();
		}
		catch (Exception ex)
		{
			Logger.Fail($"Unhandled exception while generating binary waterfall: {ex}");
		}
	}

	private static bool ParseCommandLineArguments(IEnumerable<string> args = null)
	{
		Logger.Info("Parsing command line arguments…");

		foreach (var arg in args ?? Environment.GetCommandLineArgs()[1..])
		{
			Logger.Debug($"Parsing command line argument: `{arg}`");

			if (!arg.StartsWith('-'))
			{
				if (_generator.InputFilePath != null)
				{
					Logger.Error("Cannot specify more than two input files.");
					return false;
				}
				_generator.InputFilePath = arg;
				continue;
			}

			var argKvp = arg.Split('=');

			if (argKvp[0] == "--help" || argKvp[0] == "-h" || argKvp[0] == "-?")
			{
				_helpMode = true;
				continue;
			}

			var targetParam = Utils.GetPropertyFromCliArgument(argKvp[0]);

			if (targetParam == null)
			{
				Logger.Error($"Unknown argument: `{argKvp[0]}`.");
				return false;
			}

			// Can't do a `switch` statement here. :(
			if (targetParam.DeclaringType == typeof(Generator))
			{
				if (!CliParameterAttribute.SetPropertyFromCliArgument(targetParam, _generator, argKvp[1]))
				{
					return false;
				}
			}
			else if (targetParam.DeclaringType.GetInterfaces().Contains(typeof(IExporter)))
			{
				_generator.AdditionalCliArguments.Add(argKvp[0], argKvp[1]);
				continue;
			}
			else
			{
				Logger.Error($"Cannot set property `{targetParam.Name}` because the instance of its declaring type is unknown.");
				return false;
			}
		}

		return true;
	}

	private static void PrintHelp()
	{
		StringBuilder helpStrBld = new();
		helpStrBld.AppendLine("Usage:");
		helpStrBld.AppendLine($"	{Path.GetFileName(Environment.GetCommandLineArgs()[0])} <file_input> [options]");
		helpStrBld.AppendLine();
		helpStrBld.AppendLine("Options:");
		helpStrBld.AppendLine($"	-h, -?, --help\n		Print this help text and exit");

		void AppendCommandLineArgument(PropertyInfo prop, int indentation = 1)
		{
			var cliParamAttr = prop.GetCustomAttribute<CliParameterAttribute>();
			helpStrBld.Append(new string('\t', indentation));
			if (cliParamAttr.ShortParameterName.HasValue)
			{
				helpStrBld.Append($"-{cliParamAttr.ShortParameterName}, ");
			}
			helpStrBld.Append($"--{cliParamAttr.LongParameterName}=<{prop.PropertyType.Name}> ".PadRight(cliParamAttr.ShortParameterName.HasValue ? 28 : 32));
			helpStrBld.AppendLine(cliParamAttr.Name);
			if (cliParamAttr.Description != null)
			{
				helpStrBld.AppendLine($"{new string('\t', indentation + 1)}{cliParamAttr.Description}");
			}
		}

		foreach (var prop in Utils.GetPropertiesWithAttribute<CliParameterAttribute>(typeof(Generator)))
		{
			AppendCommandLineArgument(prop);
		}
		helpStrBld.AppendLine();

		helpStrBld.AppendLine("Available parsers/input formats:");
		foreach (var parserKvp in Utils.GetTypesWithAttribute<ParserAttribute>())
		{
			var parserAttr = parserKvp.Key;
			helpStrBld.AppendLine($"	{parserAttr.Id.PadRight(16)} {parserAttr.Name}");
		}
		helpStrBld.AppendLine();

		helpStrBld.AppendLine("Available exporters:");
		foreach (var exporterKvp in Utils.GetTypesWithAttribute<ExporterAttribute>())
		{
			var exporterAttr = exporterKvp.Key;
			helpStrBld.AppendLine($"	{exporterAttr.Id.PadRight(16)} {exporterAttr.Name} – {exporterAttr.Description}");

			var cliParams = Utils.GetPropertiesWithAttribute<CliParameterAttribute>(exporterKvp.Value).ToList();
			if (cliParams.Count > 0)
			{
				helpStrBld.AppendLine($"		Options:");
				foreach (var cliParam in cliParams)
				{
					AppendCommandLineArgument(cliParam, 3);
				}
				helpStrBld.AppendLine();
			}
		}

		Console.Error.WriteLine(helpStrBld);
	}
}
