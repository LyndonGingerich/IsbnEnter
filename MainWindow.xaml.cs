using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace IsbnEnter;

public partial class MainWindow {
  private static readonly HttpClient HttpClient = new();

  public static readonly DependencyProperty IsbnProperty =
    DependencyProperty.Register(
      nameof(Isbn), typeof(CheckedIsbn?), typeof(MainWindow), new PropertyMetadata());

  private readonly CsvWriter _csvWriter;

  private readonly StreamWriter _streamWriter;

  public MainWindow() {
    InitializeComponent();
    _streamWriter = new StreamWriter("records.csv");
    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "|" };
    _csvWriter = new CsvWriter(_streamWriter, csvConfig);
    InitializeCsv();
  }

  private CheckedIsbn? Isbn {
    get => (CheckedIsbn?) GetValue(IsbnProperty);
    set => SetValue(IsbnProperty, value);
  }

  private async void CheckIsbn(object sender, TextChangedEventArgs e) {
    var isbnString = ((TextBox) sender).Text;
    if (CheckedIsbn.Create(isbnString) is { } isbn
        && await ParseJson(isbn) is { } parsedJson) {
      Isbn = isbn;
      TitleText.Text = parsedJson.Title;
      AuthorsText.Text = parsedJson.Authors;
    }
    else {
      Isbn = null;
      TitleText.Text = "";
      AuthorsText.Text = "";
    }
  }

  private static async Task<ParsedJson?> ParseJson(CheckedIsbn isbn) {
    var url = "https://www.googleapis.com/books/v1/volumes?q=isbn:" + isbn.Value;
    var json = await HttpClient.GetStringAsync(url);
    if (JsonNode.Parse(json) is not { } node) return null;
    if ((string?) ((dynamic) node)["items"]?[0]?["volumeInfo"]?["title"] is not { } title) return null;
    if ((JsonArray?) ((dynamic) node)["items"]?[0]?["volumeInfo"]?["authors"] is not { } authors) return null;
    return new ParsedJson(title, FormatAuthors(authors));
  }

  private static string FormatAuthors(JsonArray authors) {
    static string PlaceLastNameFirst(string input) {
      var elements = input.Split(" ");
      var ordered = elements.Take(elements.Length - 1).Prepend($"{elements.Last()},");
      return string.Join(" ", ordered);
    }

    var names =
      from name in
        from name in authors
        select name?.ToString()
      where name is not null
      select PlaceLastNameFirst(name);

    return string.Join("; ", names);
  }

  private void InitializeCsv() {
    _csvWriter.WriteHeader<CsvEntry>();
    _csvWriter.NextRecord();
  }

  private void WriteCsv(object sender, KeyEventArgs e) {
    if (e.Key != Key.Enter) return;

    WriteCsv();
  }

  private void WriteCsv() {
    if (!int.TryParse(CallNumberText.Text, out var callNumber)) {
      ErrorText.Text =
        CallNumberText.Text == ""
          ? "No call number specified."
          : $"{CallNumberText.Text} is not a valid integer.";
      return;
    }

    if (Isbn is not { } isbn) {
      ErrorText.Text = $"No book found with ISBN {IsbnText.Text}.";
      return;
    }

    var title = TitleText.Text;
    if (title == "") {
      ErrorText.Text = "No title specified.";
      return;
    }

    if (AuthorsText.Text == "") {
      ErrorText.Text = "No authors specified.";
      return;
    }

    var csvEntry = new CsvEntry
      { CallNumber = callNumber, Isbn = isbn.Value, Title = title, Authors = AuthorsText.Text };
    WriteSuccess(csvEntry);

    _csvWriter.WriteRecord(csvEntry);
    _csvWriter.NextRecord();

    IsbnText.Text = "";
    CallNumberText.Text = "";
    TitleText.Text = "";
    AuthorsText.Text = "";

    Keyboard.Focus(IsbnText);
  }

  private void WriteSuccess(CsvEntry entry) =>
    SuccessText.Text =
      $"Saved:\nCall number: {entry.CallNumber}\nTitle: {entry.Title}\nAuthors: {entry.Authors}\nIsbn: {entry.Isbn}";

  private void DisposeFileWriters(object? sender, EventArgs e) {
    _csvWriter.Dispose();
    _streamWriter.Dispose();
  }

  private void AdvanceToCallNumber(object sender, KeyEventArgs e) {
    if (e.Key != Key.Enter) return;
    Keyboard.Focus(CallNumberText);
  }

  private void FocusIsbnText(object sender, RoutedEventArgs e) => Keyboard.Focus(IsbnText);

  private void WriteWithoutValidating(object sender, RoutedEventArgs e) {
    const string blankIsbn = "[None]";
    Isbn = CheckedIsbn.CreateWithoutValidating(IsbnText.Text == "" ? blankIsbn : IsbnText.Text);
    WriteCsv();
  }
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
  public static CheckedIsbn CreateWithoutValidating(string isbn) => new(isbn);
}

internal readonly record struct ParsedJson(string Title, string Authors) {
  public string Title { get; } = Title;
  public string Authors { get; } = Authors;
}

internal readonly record struct CsvEntry {
  [Index(0)]
  public int CallNumber { get; init; }

  [Index(1)]
  public string Title { get; init; }

  [Index(2)]
  public string Authors { get; init; }

  [Index(3)]
  public string Isbn { get; init; }
}
