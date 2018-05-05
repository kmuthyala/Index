using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using System.Diagnostics;
using System.Reflection;

namespace index
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Tokenizer(args[0], args[1]);//Command line arguments 
            }
            catch (Exception e)
            {
                Tokenizer(@"C:\Users\K25120\Desktop\Personal\Spoo\files", @"C:\Users\K25120\Desktop\Personal\Spoo\output");
                Console.WriteLine("Error", e);
            }
            Console.WriteLine("done...");
            Console.ReadKey();
        }

        private static Dictionary<string, string> _stopWords = new Dictionary<string, string>(); // dictionary to hold stop words
        private static Dictionary<string, Token> _tokens = new Dictionary<string, Token>();// global dictionary to hold all tokens in corpus
        private static string _fileName = string.Empty;
        private static int _totalDocuments = 503;
        public static void Tokenizer(string inputPath, string outputPath)
        {
            string path = @"C:\Users\K25120\Desktop\Personal\Spoo\StopWords.txt";
            using (var file = new System.IO.StreamReader(path))
            {
                string line = string.Empty;
                while ((line = file.ReadLine()) != null)
                {
                    _stopWords.Add(line.Trim(), line); // load all stopwords into dictionary from text file
                }
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            float cputime = 0;

            DirectoryInfo directory = new DirectoryInfo(inputPath);
            int fileCounter = 0;
            foreach (FileInfo file in directory.GetFiles("*.html"))
            {
                fileCounter++;
                _fileName = file.Name;
                if (fileCounter > _totalDocuments) break;
                var tokens = new Dictionary<string, int>(); // local dictionary to process tokens for each document

                var input = File.ReadAllText(file.FullName);// read text from html file
                input = HttpUtility.HtmlDecode(input);// perform html decode to extract any special characters that were encoded
                input = ExtractEmailAddresses(input, tokens); // extract email addresses and then remove from input using regex
                input = ExtractUrls(input, tokens);
                input = RemoveHTMLTags(input);// remove html tags using regex
                input = RemovePunctuations(input); // Remove Punctuations from text using regex
                input = Regex.Replace(input, "([.]{2,})*", "", RegexOptions.IgnoreCase);
                input = input.Replace("\n", " ").Replace("\t", " ").Replace("_", " ").Trim(); // remove newline,tabs,period and underscore

                foreach (var token in input.Split(' '))
                {
                    AddToTokensDict(tokens, token);
                }

                // add per file tokens to Global dictionary which contains all tokens (from all files)
                foreach (var kvp in tokens)
                {
                    Token token;
                    if (_tokens.ContainsKey(kvp.Key))
                    {
                        // if control is here it means, token already exists in global dictionary. increment its corpusfrequency property and an entry to corpusoccurences dictionary
                        token = _tokens[kvp.Key];
                        if (!token.corpusOccurences.ContainsKey(_fileName)) // if token
                        {
                            token.corpusFrequency++;
                            token.corpusOccurences.Add(file.Name, new TokenStats() { FrequencyInDoc = kvp.Value, TotalTokensInDoc = tokens.Count() });
                        }
                    }
                    else
                    {
                        // if token does not exist in the dictionary, create an entry for this token in global dictionary
                        token = new Token()
                        {
                            Name = kvp.Key,
                            corpusFrequency = 1,
                            corpusOccurences = new Dictionary<string, TokenStats>() // 
                            { 
                                {
                                    file.Name,
                                    new TokenStats() {  FrequencyInDoc = kvp.Value, TotalTokensInDoc = tokens.Count()}
                                }
                            }
                        };
                        _tokens.Add(kvp.Key, token);
                    }
                }
            }

            WriteToFile(outputPath);

            Console.WriteLine("Time Elapsed: " + (float)sw.ElapsedMilliseconds / 1000 + " sec");
            sw.Stop();
        }

        private static string RemoveHTMLTags(string input)//To Remove HTML tags
        {
            string htmlTags = @"<[a-zA-Z0-9]*\b[^>]*>|</[a-zA-Z0-9]*\b[^>]*>";
            return Regex.Replace(input, htmlTags, " ", RegexOptions.IgnoreCase);
        }
        private static string RemovePunctuations(string input)//Remove Punctuations
        {
            string puntuations = @"[^\w\s.]";
            return Regex.Replace(input, puntuations, " ", RegexOptions.IgnoreCase);
        }

        private static string ExtractUrls(string input, Dictionary<string, int> tokens)//Handle URL's
        {
            string urls = @"http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
            var urlRegex = new Regex(urls, RegexOptions.IgnoreCase);
            MatchCollection urlMatches = urlRegex.Matches(input);

            foreach (Match match in urlMatches)
            {
                AddToTokensDict(tokens, match.Value);
            }
            return Regex.Replace(input, urls, " ", RegexOptions.IgnoreCase);
        }

        private static string ExtractEmailAddresses(string input, Dictionary<string, int> tokens)//Handle Email Addresses
        {
            string emailAddresses = @"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*";
            var emailRegex = new Regex(emailAddresses, RegexOptions.IgnoreCase);
            MatchCollection emailMatches = emailRegex.Matches(input);

            foreach (Match match in emailMatches)
            {
                AddToTokensDict(tokens, match.Value);
            }
            return Regex.Replace(input, emailAddresses, " ", RegexOptions.IgnoreCase);
        }

        private static void AddToTokensDict(Dictionary<string, int> tokens, string token)//Adds tokens to Dictionary
        {
            string tempToken = token.Trim().TrimStart('.').TrimEnd('.').ToLower();

            if (tempToken.Length > 1 && !_stopWords.ContainsKey(tempToken))
            {
                if (!string.IsNullOrEmpty(tempToken))
                {
                    if (tokens.ContainsKey(tempToken))
                        tokens[tempToken] = ++tokens[tempToken];
                    else
                        tokens.Add(tempToken, 1);
                }
            }
        }

        //Write Tokens to File
        private static void WriteToFile(string folderPath)
        {
            int postingFileIndexCount = 1; // counter to keep track of posting file index. we increment this for every token so that we can get the first occurence o
            var dictsb = new StringBuilder();
            var postingsb = new StringBuilder();

            foreach (var kvp in _tokens.OrderBy(t => t.Key))
            {

                if (kvp.Value.corpusOccurences.Count == 1)
                {
                    bool skip = false;
                    foreach(var temp in kvp.Value.corpusOccurences)
                    {
                        if (temp.Value.FrequencyInDoc == 1)
                            skip = true;
                    }
                    if (skip)
                        continue;
                }

                foreach (var kvp1 in kvp.Value.corpusOccurences)
                {
                    var tokenStat = kvp1.Value;
                    double tf = (double)tokenStat.FrequencyInDoc / tokenStat.TotalTokensInDoc;
                    double idf = Math.Log10((double)_totalDocuments / kvp.Value.corpusFrequency);
                    double tokenWeight = tf * idf;

                    postingsb.AppendLine(kvp1.Key + ", " + tokenWeight);// kvp1.key is docId/docName
                }

                dictsb.AppendLine(kvp.Key);
                dictsb.AppendLine(kvp.Value.corpusFrequency.ToString());
                dictsb.AppendLine(postingFileIndexCount.ToString());
                postingFileIndexCount += kvp.Value.corpusOccurences.Count;
            }

            using (var fs = new FileStream(Path.Combine(folderPath, "dict.txt"), FileMode.Create))
            {
                using (var outputFile = new StreamWriter(fs))
                {
                    outputFile.Write(dictsb.ToString());
                }
            }
            using (var fs = new FileStream(Path.Combine(folderPath, "postings.txt"), FileMode.Create))
            {
                using (var outputFile = new StreamWriter(fs))
                {
                    outputFile.Write(postingsb.ToString());
                }
            }

        }
    }

    public class Document
    {
        public string Name { get; set; }
        public Dictionary<string, Token> tokens = new Dictionary<string, Token>();
    }
    public class Token
    {
        public string Name { get; set; } // term 
        public int corpusFrequency { get; set; } // number of documents in which this term occurs
        public Dictionary<string, TokenStats> corpusOccurences = new Dictionary<string, TokenStats>(); // key is documentName and Value is object which tells frequency of token in that document and total tokens in that document
    }

    public class TokenStats
    {
        public int FrequencyInDoc { get; set; }
        public int TotalTokensInDoc { get; set; }
    }
}
