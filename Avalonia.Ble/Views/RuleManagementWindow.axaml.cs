using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using System; // Added for EventArgs
using Avalonia.Media; // Added for IBrush, Color, SolidColorBrush, Pen
using System.Diagnostics; // Added for Debug.WriteLine

namespace Avalonia.Ble.Views
{
    public partial class RuleManagementWindow : Window
    {
        private TextEditor _textEditor;
        private RegistryOptions _registryOptions;
        private TextMate.Installation _textMateInstallation;

        public RuleManagementWindow()
        {
            InitializeComponent();
#if DEBUG
            // this.AttachDevTools(); 
#endif
            _textEditor = this.FindControl<TextEditor>("RuleTextEditor");

            if (_textEditor != null)
            {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
                _textMateInstallation.AppliedTheme += TextMateInstallationOnAppliedTheme; // Hook up the event

                // --- Debug: List all available languages and their extensions ---
                Debug.WriteLine("[TextMate] Listing all available languages from RegistryOptions:");
                var allLanguages = _registryOptions.GetAvailableLanguages();
                if (allLanguages != null)
                {
                    foreach (var lang in allLanguages)
                    {
                        string extensions = lang.Extensions != null ? string.Join(", ", lang.Extensions) : "(no extensions)";
                        Debug.WriteLine($"[TextMate] Language ID: {lang.Id}, Extensions: [{extensions}]");
                    }
                }
                else
                {
                    Debug.WriteLine("[TextMate] No languages found in RegistryOptions.");
                }
                Debug.WriteLine("[TextMate] --- End of language listing ---");
                // --- End Debug ---

                // Attempt to load C# grammar
                var csharpLanguage = _registryOptions.GetLanguageByExtension(".cs"); // Changed from .json to .cs
                if (csharpLanguage != null)
                {
                    Debug.WriteLine($"[TextMate] Found language ID for C#: {csharpLanguage.Id}"); 
                    string scopeName = _registryOptions.GetScopeByLanguageId(csharpLanguage.Id);
                    Debug.WriteLine($"[TextMate] Scope name for C#: {scopeName}"); 
                    if (!string.IsNullOrEmpty(scopeName))
                    {
                        _textMateInstallation.SetGrammar(scopeName);
                        Debug.WriteLine($"[TextMate] Set grammar to C#: {scopeName}");
                    }
                    else
                    {
                        Debug.WriteLine("[TextMate] Error: Scope name for C# is null or empty.");
                    }
                }
                else
                {
                    Debug.WriteLine("[TextMate] C# language not found for TextMate highlighting. Ensure TextMateSharp.Grammars is correctly referenced and includes C#.");
                }
                // Apply the theme colors initially
                TextMateInstallationOnAppliedTheme(null, _textMateInstallation);
            }
            else
            {
                Debug.WriteLine("RuleTextEditor not found.");
            }
        }

        private void TextMateInstallationOnAppliedTheme(object sender, TextMate.Installation e)
        {
            if (e == null) return;
            ApplyThemeColorsToEditor(e);
        }

        void ApplyThemeColorsToEditor(TextMate.Installation tmInstallation)
        {
            ApplyBrushAction(tmInstallation, "editor.background", brush => _textEditor.Background = brush);
            ApplyBrushAction(tmInstallation, "editor.foreground", brush => _textEditor.Foreground = brush);

            if (!ApplyBrushAction(tmInstallation, "editor.selectionBackground",
                    brush => _textEditor.TextArea.SelectionBrush = brush))
            {
                // Fallback if not in theme
                if (Application.Current!.TryGetResource("TextAreaSelectionBrush", out var resourceObject) && resourceObject is IBrush fallbackBrush)
                {
                    _textEditor.TextArea.SelectionBrush = fallbackBrush;
                }
            }

            if (!ApplyBrushAction(tmInstallation, "editor.lineHighlightBackground",
                    brush =>
                    {
                        _textEditor.TextArea.TextView.CurrentLineBackground = brush;
                        _textEditor.TextArea.TextView.CurrentLineBorder = new Pen(brush);
                    }))
            {
                 // Fallback: In AvaloniaEdit 0.10.12, SetDefaultHighlightLineColors might not exist.
                 // Manually set to a default or leave as is if not critical.
                 // For example, set to a transparent or very light gray if needed.
                 // _textEditor.TextArea.TextView.CurrentLineBackground = Brushes.Transparent;
                 // _textEditor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.LightGray);
            }

            if (!ApplyBrushAction(tmInstallation, "editorLineNumber.foreground",
                    brush => _textEditor.LineNumbersForeground = brush))
            {
                _textEditor.LineNumbersForeground = _textEditor.Foreground; // Fallback to editor foreground
            }
        }

        bool ApplyBrushAction(TextMate.Installation tmInstallation, string colorKeyNameFromJson, Action<IBrush> applyColorAction)
        {
            if (tmInstallation == null || !tmInstallation.TryGetThemeColor(colorKeyNameFromJson, out var colorString))
                return false;

            if (!Color.TryParse(colorString, out Color color))
                return false;

            var colorBrush = new SolidColorBrush(color);
            applyColorAction(colorBrush);
            return true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _textMateInstallation?.Dispose(); // Dispose of TextMate installation
        }
    }
}
