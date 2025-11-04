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
        private ComboBox _syntaxModeCombo;
        private ComboBox _themeCombo; // Added ComboBox field for themes
        private RegistryOptions _registryOptions;
        private TextMate.Installation _textMateInstallation;

        // Define available themes (updated based on likely available ThemeName members)
        private readonly ThemeName[] _availableThemes = new[]
        {
            ThemeName.DarkPlus,      // Commonly available
            ThemeName.LightPlus,     // Commonly available
            ThemeName.Dark,          // Commonly available
            ThemeName.Light,         // Commonly available
            ThemeName.Monokai,       // Commonly available
            ThemeName.SolarizedDark, // Often available
            ThemeName.SolarizedLight,// Often available
            ThemeName.Red,           // Often available
            ThemeName.QuietLight,    // Often available
            ThemeName.TomorrowNightBlue // Often available
        };

        public RuleManagementWindow()
        {
            InitializeComponent();
#if DEBUG
            // this.AttachDevTools(); 
#endif
            _textEditor = this.FindControl<TextEditor>("RuleTextEditor");
            _syntaxModeCombo = this.FindControl<ComboBox>("SyntaxModeCombo");
            _themeCombo = this.FindControl<ComboBox>("ThemeCombo"); // Get ThemeCombo instance

            if (_textEditor != null && _syntaxModeCombo != null && _themeCombo != null)
            {
                // Initialize with a default theme (e.g., DarkPlus)
                var initialTheme = ThemeName.DarkPlus;
                _registryOptions = new RegistryOptions(initialTheme);
                _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
                _textMateInstallation.AppliedTheme += TextMateInstallationOnAppliedTheme;

                // Populate SyntaxModeCombo
                var allLanguages = _registryOptions.GetAvailableLanguages();
                _syntaxModeCombo.ItemsSource = allLanguages;
                _syntaxModeCombo.DisplayMemberBinding = new Avalonia.Data.Binding("Id");

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

                // Set initial language
                var csharpLanguage = allLanguages?.FirstOrDefault(lang => lang.Id == "csharp" || (lang.Extensions != null && lang.Extensions.Contains(".cs")));
                if (csharpLanguage != null)
                {
                    _syntaxModeCombo.SelectedItem = csharpLanguage;
                    string initialScopeName = _registryOptions.GetScopeByLanguageId(csharpLanguage.Id);
                    if (!string.IsNullOrEmpty(initialScopeName))
                    {
                        _textMateInstallation.SetGrammar(initialScopeName);
                        Debug.WriteLine($"[TextMate] Initial grammar set to C#: {initialScopeName}");
                    }
                }
                else
                {
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
                _syntaxModeCombo.SelectionChanged += SyntaxModeCombo_SelectionChanged;

                // Populate ThemeCombo
                _themeCombo.ItemsSource = _availableThemes;
                _themeCombo.SelectedItem = initialTheme; // Set initial theme selection
                _themeCombo.SelectionChanged += ThemeCombo_SelectionChanged; // Subscribe to event

                TextMateInstallationOnAppliedTheme(null, _textMateInstallation);
            }
            else
            {
                Debug.WriteLine("RuleTextEditor, SyntaxModeCombo, or ThemeCombo not found.");
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
                _syntaxModeCombo.SelectionChanged -= SyntaxModeCombo_SelectionChanged;
            }
            if (_themeCombo != null) // Unsubscribe from ThemeCombo event
            {
                _themeCombo.SelectionChanged -= ThemeCombo_SelectionChanged;
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

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_textEditor == null || e.AddedItems.Count == 0)
                return;

            if (e.AddedItems[0] is ThemeName selectedThemeName)
            {
                Debug.WriteLine($"[TextMate] Theme changing to: {selectedThemeName}");

                // Dispose previous installation
                _textMateInstallation?.Dispose();
                _textMateInstallation = null;

                // Create new registry options with the new theme
                _registryOptions = new RegistryOptions(selectedThemeName);
                
                // Re-install TextMate
                _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
                if (_textMateInstallation != null)
                {
                    _textMateInstallation.AppliedTheme += TextMateInstallationOnAppliedTheme;
                    Debug.WriteLine($"[TextMate] TextMate re-installed with theme: {selectedThemeName}");

                    // Re-apply current grammar
                    if (_syntaxModeCombo.SelectedItem is Language selectedLanguage)
                    {
                        string scopeName = _registryOptions.GetScopeByLanguageId(selectedLanguage.Id);
                        if (!string.IsNullOrEmpty(scopeName))
                        {
                            _textMateInstallation.SetGrammar(scopeName);
                            Debug.WriteLine($"[TextMate] Grammar '{selectedLanguage.Id}' re-applied after theme change.");
                        }
                        else
                        {
                            Debug.WriteLine($"[TextMate] Error: Could not get scope name for {selectedLanguage.Id} after theme change.");
                             _textMateInstallation.SetGrammar(null); // Clear grammar
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[TextMate] No language selected in SyntaxModeCombo after theme change. Clearing grammar.");
                        _textMateInstallation.SetGrammar(null); // Clear grammar if no language is selected
                    }
                    
                    // Manually trigger theme application to update colors
                    TextMateInstallationOnAppliedTheme(this, _textMateInstallation);
                    Debug.WriteLine($"[TextMate] Theme colors re-applied for: {selectedThemeName}");
                }
                else
                {
                    Debug.WriteLine($"[TextMate] Error: Failed to re-install TextMate with theme: {selectedThemeName}");
                }
            }
        }
    }
}
