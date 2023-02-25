using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace IsbnEnter;

public partial class MainWindow {
  private static readonly HttpClient HttpClient = new();

  public static readonly DependencyProperty IsbnStringProperty =
    DependencyProperty.Register(
      nameof(IsbnString), typeof(string), typeof(MainWindow), new PropertyMetadata(CheckIsbn));

  public static readonly DependencyProperty IsbnProperty =
    DependencyProperty.Register(
      nameof(Isbn), typeof(CheckedIsbn?), typeof(MainWindow), new PropertyMetadata());

  private readonly CsvWriter _csvWriter;

  private readonly StreamWriter _streamWriter;

  public MainWindow() {
    InitializeComponent();
    _streamWriter = new StreamWriter("records.csv");
    _csvWriter = new CsvWriter(_streamWriter, CultureInfo.InvariantCulture);
    InitializeCsv();
  }

  private string IsbnString {
    get => (string) GetValue(IsbnStringProperty);
    set => SetValue(IsbnStringProperty, value);
  }

  private CheckedIsbn? Isbn {
    get => (CheckedIsbn?) GetValue(IsbnProperty);
    set => SetValue(IsbnProperty, value);
  }

  private static async void CheckIsbn(DependencyObject d, DependencyPropertyChangedEventArgs e) {
    var window = (MainWindow) d;
    var isbnString = (string) e.NewValue;
    if (CheckedIsbn.Create(isbnString) is { } isbn
        && await ParseJson(isbn) is { } parsedJson) {
      window.Isbn = isbn;
      window.TitleText.Text = parsedJson.Title;
    }
    else {
      window.Isbn = null;
      window.TitleText.Text = "";
    }
  }

  private static async Task<ParsedJson?> ParseJson(CheckedIsbn isbn) {
    var url = "https://www.googleapis.com/books/v1/volumes?q=isbn:" + isbn.Value;
    var json = await HttpClient.GetStringAsync(url);
    if (JsonNode.Parse(json) is not { } node) return null;
    if ((string?) ((dynamic) node)["items"]?[0]?["volumeInfo"]?["title"] is not { } title) return null;
    return new ParsedJson(title);
  }

  private void InitializeCsv() {
    _csvWriter.WriteHeader<CsvEntry>();
    _csvWriter.NextRecord();
  }

  private void WriteCsv(object sender, KeyEventArgs e) {
    if (e.Key != Key.Enter) return;
    if (!int.TryParse(CallNumberText.Text, out var callNumber)) return;
    if (Isbn is not { } isbn) return;

    var title = TitleText.Text;
    if (title == "") return;

    _csvWriter.WriteRecord(new CsvEntry { CallNumber = callNumber, Isbn = isbn.Value, Title = title });
    _csvWriter.NextRecord();

    IsbnText.Text = "";
    CallNumberText.Text = "";

    Keyboard.Focus(IsbnText);
  }

  private void DisposeFileWriters(object? sender, EventArgs e) {
    _csvWriter.Dispose();
    _streamWriter.Dispose();
  }

  private void AdvanceToCallNumber(object sender, KeyEventArgs e) {
    if (e.Key != Key.Enter) return;
    Keyboard.Focus(CallNumberText);
  }

  private void FocusIsbnText(object sender, RoutedEventArgs e) => Keyboard.Focus(IsbnText);
}

internal readonly record struct CheckedIsbn {
  private static readonly char[] ValidChars = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'X' };

  private CheckedIsbn(string isbn) {
    Value = isbn;
  }

  public string Value { get; }

  private static bool IsFormattedAsIsbn(string s) =>
    s.AsEnumerable().All(c => ValidChars.Contains(c)) && s.Length is 10 or 13;

  public static CheckedIsbn? Create(string isbn) => IsFormattedAsIsbn(isbn) ? new CheckedIsbn(isbn) : null;
}

internal readonly record struct ParsedJson(string Title) {
  public string Title { get; } = Title;
}

internal readonly record struct CsvEntry {
  [Index(0)]
  public int CallNumber { get; init; }

  [Index(1)]
  public string Title { get; init; }

  [Index(2)]
  public string Isbn { get; init; }
}
