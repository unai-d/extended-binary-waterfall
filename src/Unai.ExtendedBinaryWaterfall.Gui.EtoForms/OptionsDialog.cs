using System;
using System.Linq;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Unai.ExtendedBinaryWaterfall.Parsers;
using Unai.ExtendedBinaryWaterfall.Attributes;

namespace Unai.ExtendedBinaryWaterfall.Gui.EtoForms;

public class OptionsDialog : Dialog
{
	internal Generator _generator;

	readonly TabControl _uiMain;
	readonly TabPage _uiTabInputOptions;
	readonly Scrollable _uiMainInputOptions;
	readonly TableLayout _uiTableInputOptions;

	public OptionsDialog(Generator generator)
	{
		_generator = generator;

		Title = "Binary Waterfall Options";
		Size = new Size(600, 600);
		Maximizable = false;
		Minimizable = false;

		// Create controls.

		_uiTableInputOptions = new()
		{
			Padding = new Padding(8),
			Spacing = new Size(8, 8)
		};

		_uiMainInputOptions = new() { Content = _uiTableInputOptions };

		foreach (var prop in Utils.GetPropertiesWithAttribute<CliParameterAttribute>(typeof(Generator)))
		{
			if (prop.Name == nameof(Generator.ExporterId))
			{
				continue;
			}

			Logger.Debug($"Creating controls for property `{prop.Name}`…");

			var paramAttr = prop.GetCustomAttribute<CliParameterAttribute>();

			TableRow row = new();
			row.Cells.Add(new(new Label()
			{
				Text = paramAttr.Name,
				ToolTip = paramAttr.Description
			}));
			
			Control valueCtrl = GetControlForProperty(prop);

			if (valueCtrl == null)
			{
				if (prop.PropertyType == typeof(string))
				{
					TextBox textBox;
					valueCtrl = textBox = new TextBox()
					{
						Text = (string)prop.GetValue(_generator)
					};
					textBox.TextChanged += (s, e) => prop.SetValue(_generator, textBox.Text);
				}
				else if (prop.PropertyType == typeof(int))
				{
					NumericStepper numStepper;
					valueCtrl = numStepper = new NumericStepper()
					{
						Value = (int)prop.GetValue(_generator)
					};
					numStepper.ValueChanged += (s, e) => prop.SetValue(_generator, (int)numStepper.Value);
				}
				else if (prop.PropertyType == typeof(bool))
				{
					CheckBox checkBox;
					valueCtrl = checkBox = new CheckBox()
					{
						Checked = (bool)prop.GetValue(_generator)
					};
					checkBox.CheckedChanged += (s, e) => prop.SetValue(_generator, (bool)checkBox.Checked);
				}
				else if (prop.PropertyType.IsEnum)
				{
					DropDown dropDown;
					valueCtrl = dropDown = new DropDown();
					
					foreach (var enumVal in Enum.GetValues(prop.PropertyType))
					{
						dropDown.Items.Add(enumVal.ToString(), enumVal.ToString());
					}

					dropDown.SelectedKey = prop.GetValue(_generator).ToString();
					dropDown.SelectedKeyChanged += (s, e) => prop.SetValue(_generator, Enum.Parse(prop.PropertyType, dropDown.SelectedKey));
				}
				else
				{
					valueCtrl = new Label()
					{
						Text = "Not implemented yet.",
						TextColor = Color.FromGrayscale(0.5f)
					};
				}
			}

			row.Cells.Add(valueCtrl);

			_uiTableInputOptions.Rows.Add(row);
		}

		_uiTabInputOptions = new(_uiMainInputOptions)
		{
			Text = "Generator"
		};

		_uiMain = new();
		_uiMain.Pages.Add(_uiTabInputOptions);

		Content = _uiMain;
	}

	public Control GetControlForProperty(PropertyInfo prop)
	{
		Control ret = null;

		if (prop == typeof(Generator).GetProperty(nameof(Generator.InputFileFormatId)))
		{
			// Exporter ID.

			DropDown dropdown = new();
			ret = dropdown;
			foreach (var parser in Utils.GetTypesWithAttribute<ParserAttribute>())
			{
				dropdown.Items.Add(parser.Key.Name, parser.Key.Id);
			}
			dropdown.SelectedKey = MainForm._nullParserId;
			dropdown.SelectedKeyChanged += (s, e) =>
			{
				var parserAttr = Utils.GetTypesWithAttribute<ParserAttribute>().FirstOrDefault(pa => pa.Key.Id == dropdown.SelectedKey);
				if (parserAttr.Key == null)
				{
					// TODO: handle.
					Logger.Error("Cannot retrieve drop down item key for parser setting.");
					return;
				}
				Logger.Debug($"Selected parser ID: '{parserAttr.Key.Id}'.");
				_generator.Parser = (IParser)Activator.CreateInstance(parserAttr.Value);
			};
		}

		return ret;
	}
}
