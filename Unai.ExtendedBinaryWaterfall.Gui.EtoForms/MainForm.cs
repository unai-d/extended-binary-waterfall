using System;
using Eto.Forms;
using Eto.Drawing;
using Unai.ExtendedBinaryWaterfall.Gui.EtoForms.Exporters;
using System.Globalization;
using Unai.ExtendedBinaryWaterfall.Parsers.Custom;
using System.Reflection;
using Unai.ExtendedBinaryWaterfall.Parsers;
using System.Linq;
using Unai.ExtendedBinaryWaterfall.Exporters;
using System.Threading.Tasks;
using Eto;
using System.IO;

namespace Unai.ExtendedBinaryWaterfall.Gui.EtoForms
{
	public partial class MainForm : Form
	{
		internal Generator _generator = new();
		internal int _currentFrame = 0;
		private readonly string _nullParserId = typeof(CustomParser).GetCustomAttribute<ParserAttribute>().Id;
		private string _inputFilePath = null;
		private string _inputAuxFilePath = null;

		#region Controls

		internal TableLayout _uiMain = null;
		internal ImageView _uiViewport = null;
		internal TableLayout _uiPlayerBar = null;
		internal Label _uiPlayerBarTs = new();
		internal Slider _uiPlayHead = null;
		internal TableLayout _uiConfigPanel = null;
		internal DropDown _uiParserDropDown = null;
		internal TextBox _uiBitrate = null;

		#endregion

		#region Commands

		internal Command _cmdOpenFile, _cmdRender;

		#endregion

		internal bool FileIsOpened => _inputFilePath != null && _generator.InputFileStream != null;

		public MainForm()
		{
			// Make decimals use "." instead of other characters.
			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			
			Title = BuildInfo.ApplicationName;
			Size = new Size(1280, 720);
			MinimumSize = new Size(320, 240);

			_uiViewport = new()
			{
				BackgroundColor = Color.FromArgb(0, 0, 0),
				Image = new Bitmap(320, 240, PixelFormat.Format32bppRgba)
			};

			_uiPlayHead = new();
			_uiPlayHead.ValueChanged += (s, e) =>
			{
				_currentFrame = _uiPlayHead.Value;
				UpdateUI();
			};

			_uiPlayerBar = new()
			{
				Padding = 8,
				Spacing = new Size(8, 8),
				Rows =
				{
					new TableRow()
					{
						Cells =
						{
							_uiPlayerBarTs,
							_uiPlayHead
						}
					}
				}
			};

			_uiParserDropDown = new DropDown();
			foreach (var parser in Utils.GetTypesWithAttribute<ParserAttribute>())
			{
				_uiParserDropDown.Items.Add(parser.Key.Name, parser.Key.Id);
			}
			_uiParserDropDown.SelectedKey = _nullParserId;
			_uiParserDropDown.SelectedKeyChanged += HandleSetParser;

			_uiBitrate = new()
			{
				Text = _generator.InputBytesPerSecond.ToString()
			};
			_uiBitrate.TextChanged += HandleSetBitrate;

			_uiConfigPanel = new TableLayout()
			{
				Padding = 8,
				Spacing = new Size(8, 8),
				Rows =
				{
					new TableRow("Input file parser", "Input bitrate (bytes/s)"),
					new TableRow(_uiParserDropDown, _uiBitrate)
				}
			};

			_uiMain = new TableLayout
			{
				Spacing = new Size(8, 8),
				Rows =
				{
					TableRow.Scaled(_uiViewport),
					new TableRow(_uiPlayerBar),
					new TableRow(_uiConfigPanel)
				}
			};

			Content = _uiMain;

			_cmdOpenFile = new Command
			{
				MenuText = "&Open File…",
				Shortcut = Application.Instance.CommonModifier | Keys.O,
				Image = SystemIcons.Get(SystemIconType.OpenDirectory, SystemIconSize.Small),
			};
			_cmdOpenFile.Executed += HandleOpenFile;

			_cmdRender = new Command
			{
				MenuText = "&Render…",
				Shortcut = Application.Instance.CommonModifier | Keys.S
			};
			_cmdRender.Executed += HandleRenderCommand;

			var quitCommand = new Command { MenuText = "Quit", Shortcut = Application.Instance.CommonModifier | Keys.Q };
			quitCommand.Executed += (sender, e) => Application.Instance.Quit();

			var aboutCommand = new Command { MenuText = "About…" };
			aboutCommand.Executed += HandleShowAboutDialog;

			Menu = new MenuBar
			{
				Items =
				{
					new SubMenuItem
					{
						Text = "&File",
						Items =
						{
							_cmdOpenFile,
							_cmdRender
						}
					},
				},
				ApplicationItems =
				{
					new ButtonMenuItem { Text = "&Preferences…" },
				},
				QuitItem = quitCommand,
				AboutItem = aboutCommand
			};

			UpdateControls();
		}

		/// <summary>
		/// Updates the entire user interface.
		/// </summary>
		private void UpdateUI()
		{
			if (FileIsOpened)
			{
				Logger.Debug($"Generating frame #{_currentFrame}…");
				try
				{
					_generator.GenerateFrame(_currentFrame);
				}
				catch (Exception ex)
				{
					Logger.Fail($"Cannot update video preview: {ex}");
				}
			}
			_generator.UpdateLayout();
			UpdateControls();
		}

		private void UpdateControls()
		{
			// Update viewport.

			_uiViewport.Width = _uiViewport.LogicalParent.Width;
			if (_uiViewport.Width < 1) _uiViewport.Width = Width;
			_uiViewport.Height = ClientSize.Height - _uiPlayerBar.Height - _uiPlayerBar.Height - 32;

			// Update timestamp.

			var currentTs = TimeSpan.FromSeconds(_currentFrame / _generator.OutputFps);
			var totalTs = TimeSpan.FromSeconds(_generator.TotalFrames / _generator.OutputFps);
			_uiPlayerBarTs.Text = $"{currentTs} / {totalTs}";

			// Update timeline/playhead.

			_uiPlayHead.MinValue = 0;
			_uiPlayHead.MaxValue = (int)_generator.TotalFrames;
			_uiPlayHead.Value = _currentFrame;
		}

		private void UpdateBasedOnInputFile(string inputFilePath)
		{
			Logger.Info($"Setting input file: '{inputFilePath}'…");

			_inputFilePath = inputFilePath;
			_generator.InputFilePath = _inputFilePath;
			_generator.Exporter ??= new InternalExporter(this);
			// Force update
			_generator.InputFileStream = null;
			_generator.SubFiles = [];

			// TODO: Use Utils common method for this.
			var inputFileExtension = Path.GetExtension(_inputFilePath);
			bool formatDetected = false;
			foreach (var parser in Utils.GetTypesWithAttribute<ParserAttribute>())
			{
				if (parser.Key.FileExtensions.Contains(inputFileExtension))
				{
					_uiParserDropDown.SelectedKey = parser.Key.Id;
					formatDetected = true;
					break;
				}
			}

			if (formatDetected)
			{
				HandleSetParser(this, null);
			}

			_generator.Initialize();

			UpdateUI();
		}

		#region Command Handlers

		private void HandleOpenFile(object sender, EventArgs e)
		{
			var fileOpenDlg = new OpenFileDialog()
			{
				CheckFileExists = true,
				MultiSelect = false
			};

			var fileOpenDlgRes = fileOpenDlg.ShowDialog(this);

			Logger.Debug($"Open file dialog returned {fileOpenDlgRes}.");

			if (!fileOpenDlgRes.HasFlag(DialogResult.Ok))
			{
				return;
			}

			// TODO: Is there a way to close the dialog while this call does stuff?
			UpdateBasedOnInputFile(fileOpenDlg.FileName);
		}

		private void HandleShowAboutDialog(object sender, EventArgs e)
		{
			var abtDiag = new AboutDialog
			{
				ProgramName = BuildInfo.ApplicationName,
				Version = BuildInfo.SemVer,
				Website = new Uri("https://github.com/unai-d/extended-binary-waterfall")
			};
			abtDiag.ShowDialog(this);
		}

		private void HandleSetParser(object sender, EventArgs e)
		{
			var parserAttr = Utils.GetTypesWithAttribute<ParserAttribute>().FirstOrDefault(pa => pa.Key.Id == _uiParserDropDown.SelectedKey);
			if (parserAttr.Key == null)
			{
				// TODO: handle.
				return;
			}
			Logger.Debug($"Selected parser ID: '{parserAttr.Key.Id}'.");
			_generator.Parser = (IParser)Activator.CreateInstance(parserAttr.Value);

			try
			{
				_generator.Initialize();
			}
			catch (Exception ex)
			{
				Logger.Fail(ex.ToString());
				MessageBox.Show($"Cannot initialize parser: {ex.Message}\n{ex.StackTrace}", MessageBoxType.Error);
			}

			UpdateUI();
		}

		private void HandleSetBitrate(object sender, EventArgs e)
		{
			if (int.TryParse(_uiBitrate.Text, out var result))
			{
				if (result > 1024)
				{
					_generator.InputBytesPerSecond = result;
					_generator.Initialize();
				}

				UpdateUI();
			}
		}

		private void HandleRenderCommand(object sender, EventArgs e)
		{
			var saveFileDlg = new SaveFileDialog()
			{
				FileName = (_inputFilePath ?? "output") + ".mkv",
				Title = "Save Render Result As…"
			};

			saveFileDlg.Filters.Add(new("Matroska Video File", ".mkv"));

			var saveFileDlgRes = saveFileDlg.ShowDialog(this);

			if (!saveFileDlgRes.HasFlag(DialogResult.Ok))
			{
				return;
			}

			if (File.Exists(saveFileDlg.FileName))
			{
				MessageBox.Show("Output file already exists! Delete the file and try again, move it somewhere else, or use another filename.", MessageBoxType.Error);
				return;
			}

			var label = new Label();

			var pbBar = new ProgressBar()
			{
				MinValue = 0,
				MaxValue = 1048576,
				Value = 0,
			};

			var cancelButton = new Button()
			{
				Text = "Cancel"
			};
			cancelButton.Click += (s, e) =>
			{
				_generator.StopGeneration();
			};
			
			var stack = new StackLayout()
			{
				Spacing = 8,
				Padding = 8,
				Items =
				{
					label,
					pbBar,
					cancelButton
				},
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Center,
			};

			var dialog = new Dialog()
			{
				Title = "Render Progress",
				AbortButton = cancelButton,
				ClientSize = new Size(480, 120),
				Closeable = false,
				DisplayMode = DialogDisplayMode.Attached,
				Content = stack
			};

			_generator.Exporter = new FfmpegExporter
			{
				Generator = _generator
			};

			_generator.OnProgress += p =>
			{
				Application.Instance.InvokeAsync(() =>
				{
					pbBar.Value = (int)(p * 1048576);
					label.Text = $"{p * 100:N2} %";
				});
			};

			_generator.OnFinish += dialog.Close;

			_generator.OutputFilePath = saveFileDlg.FileName;

			_generator.Initialize();

			dialog.ShowModalAsync(this);

			Task.Run(_generator.Generate).ContinueWith(t =>
			{
				if (t.IsFaulted)
				{
					Application.Instance.Invoke(() => MessageBox.Show($"Render process failed: {t.Exception.Message}", MessageBoxType.Error));
				}
			});
		}

		#endregion

		protected override void OnSizeChanged(EventArgs e)
		{
			UpdateControls();
			base.OnSizeChanged(e);
		}
	}
}
