using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using System; // Added for EventArgs
using Avalonia.Media; // Added for IBrush, Color, SolidColorBrush, Pen
using System.Diagnostics; // Added for Debug.WriteLine
using System.Linq; // Added for LINQ

namespace Avalonia.Ble.Views
{
    public partial class RuleManagementWindow : Window
    {
        private TextEditor _textEditor;
        private ComboBox _syntaxModeCombo; // Added ComboBox field
        private RegistryOptions _registryOptions;
        private TextMate.Installation _textMateInstallation;

        public RuleManagementWindow()
        {
            InitializeComponent();
#if DEBUG
            // this.AttachDevTools(); 
#endif
            _textEditor = this.FindControl<TextEditor>("RuleTextEditor");
            _syntaxModeCombo = this.FindControl<ComboBox>("SyntaxModeCombo"); // Get ComboBox instance

            if (_textEditor != null && _syntaxModeCombo != null)
            {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
                _textMateInstallation.AppliedTheme += TextMateInstallationOnAppliedTheme;

                var allLanguages = _registryOptions.GetAvailableLanguages();
                _syntaxModeCombo.ItemsSource = allLanguages;
                _syntaxModeCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Id"); // Show language Id in ComboBox

                // --- Debug: List all available languages and their extensions ---
                Debug.WriteLine("[TextMate] Listing all available languages from RegistryOptions:");
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

                // Set initial language (e.g., C#)
                var csharpLanguage = allLanguages?.FirstOrDefault(lang => lang.Id == "csharp" || (lang.Extensions != null && lang.Extensions.Contains(".cs")));
                if (csharpLanguage != null)
                {
                    _syntaxModeCombo.SelectedItem = csharpLanguage;
                    // Load initial grammar based on selection (or can be done in SelectionChanged handler)
                    string initialScopeName = _registryOptions.GetScopeByLanguageId(csharpLanguage.Id);
                    if (!string.IsNullOrEmpty(initialScopeName))
                    {
                        _textMateInstallation.SetGrammar(initialScopeName);
                        Debug.WriteLine($"[TextMate] Initial grammar set to C#: {initialScopeName}");
                    }
                }
                else
                {
                    // Fallback if C# is not found, or select the first available language
                    if (allLanguages != null && allLanguages.Any())
                    {
                        _syntaxModeCombo.SelectedIndex = 0;
                        var firstLang = allLanguages.First();
                        string firstScopeName = _registryOptions.GetScopeByLanguageId(firstLang.Id);
                         if (!string.IsNullOrEmpty(firstScopeName))
                        {
                            _textMateInstallation.SetGrammar(firstScopeName);
                            Debug.WriteLine($"[TextMate] Initial grammar set to first available: {firstScopeName}");
                        }
                    }
                    else
                    {
                         Debug.WriteLine("[TextMate] C# language not found and no other languages available to set as initial.");
                    }
                }

                _syntaxModeCombo.SelectionChanged += SyntaxModeCombo_SelectionChanged; // Subscribe to event

                TextMateInstallationOnAppliedTheme(null, _textMateInstallation);
            }
            else
            {
                Debug.WriteLine("RuleTextEditor or SyntaxModeCombo not found.");
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
            if (_syntaxModeCombo != null)
            {
                _syntaxModeCombo.SelectionChanged -= SyntaxModeCombo_SelectionChanged; // Unsubscribe
            }
            _textMateInstallation?.Dispose();
        }

        private void SyntaxModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_textMateInstallation == null || _registryOptions == null || e.AddedItems.Count == 0)
                return;

            if (e.AddedItems[0] is Language selectedLanguage)
            {
                string scopeName = _registryOptions.GetScopeByLanguageId(selectedLanguage.Id);
                if (!string.IsNullOrEmpty(scopeName))
                {
                    _textMateInstallation.SetGrammar(scopeName);
                    Debug.WriteLine($"[TextMate] Grammar changed to: {selectedLanguage.Id} ({scopeName})");
                    // Optionally, you might want to clear and reload the document or provide sample text
                    // _textEditor.Document.Text = $"// Switched to {selectedLanguage.Id}"; 
                }
                else
                {
                    Debug.WriteLine($"[TextMate] Error: Scope name for {selectedLanguage.Id} is null or empty.");
                    _textMateInstallation.SetGrammar(null); // Clear grammar if scope is not found
                }
            }
        }
    }
}
