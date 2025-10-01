using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    /// <summary>
    /// Provides simple text tokenization and detokenization for ML inference.
    /// Supports basic BPE-style tokenization with special tokens.
    /// </summary>
    public sealed class TokenizerService
    {
        // Special tokens commonly used in transformer models
        private const int PadTokenId = 0;
        private const int UnkTokenId = 1;
        private const int BosTokenId = 2; // Beginning of sequence
        private const int EosTokenId = 3; // End of sequence
        
        private const string PadToken = "[PAD]";
        private const string UnkToken = "[UNK]";
        private const string BosToken = "[BOS]";
        private const string EosToken = "[EOS]";

        // Simple vocabulary based on character-level encoding for universal support
        // This can be extended to use more sophisticated tokenizers
        private readonly Dictionary<string, int> _vocab;
        private readonly Dictionary<int, string> _reverseVocab;
        private readonly Regex _tokenPattern;

        public TokenizerService()
        {
            _vocab = new Dictionary<string, int>();
            _reverseVocab = new Dictionary<int, string>();
            
            // Initialize with special tokens
            _vocab[PadToken] = PadTokenId;
            _vocab[UnkToken] = UnkTokenId;
            _vocab[BosToken] = BosTokenId;
            _vocab[EosToken] = EosTokenId;

            _reverseVocab[PadTokenId] = PadToken;
            _reverseVocab[UnkTokenId] = UnkToken;
            _reverseVocab[BosTokenId] = BosToken;
            _reverseVocab[EosTokenId] = EosToken;

            // Build a basic vocabulary with common characters and byte-level fallback
            // This ensures we can tokenize any text
            var nextId = 4;

            // Add common ASCII printable characters
            for (var c = 32; c < 127; c++)
            {
                var token = ((char)c).ToString();
                _vocab[token] = nextId;
                _reverseVocab[nextId] = token;
                nextId++;
            }

            // Add common whitespace and newline
            _vocab["\n"] = nextId++;
            _reverseVocab[nextId - 1] = "\n";
            _vocab["\r"] = nextId++;
            _reverseVocab[nextId - 1] = "\r";
            _vocab["\t"] = nextId++;
            _reverseVocab[nextId - 1] = "\t";

            // Pattern to split text into tokens (words and punctuation)
            _tokenPattern = new Regex(
                @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+",
                RegexOptions.Compiled | RegexOptions.CultureInvariant
            );
        }

        /// <summary>
        /// Encodes text into token IDs.
        /// </summary>
        /// <param name="text">The text to encode</param>
        /// <param name="maxLength">Maximum sequence length (0 for no limit)</param>
        /// <param name="addSpecialTokens">Whether to add BOS/EOS tokens</param>
        /// <param name="padding">Whether to pad to maxLength</param>
        /// <returns>Array of token IDs</returns>
        public long[] Encode(
            string text,
            int maxLength = 512,
            bool addSpecialTokens = true,
            bool padding = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return addSpecialTokens ? new[] { (long)BosTokenId, (long)EosTokenId } : Array.Empty<long>();
            }

            var tokens = new List<long>();

            if (addSpecialTokens)
            {
                tokens.Add(BosTokenId);
            }

            // Tokenize using pattern matching
            var matches = _tokenPattern.Matches(text);
            foreach (Match match in matches)
            {
                var token = match.Value;
                
                // Try to encode as a single token first
                if (_vocab.TryGetValue(token, out var tokenId))
                {
                    tokens.Add(tokenId);
                }
                else
                {
                    // Fallback: encode character by character
                    foreach (var ch in token)
                    {
                        var charStr = ch.ToString();
                        if (_vocab.TryGetValue(charStr, out var charId))
                        {
                            tokens.Add(charId);
                        }
                        else
                        {
                            // Unknown character - use byte-level encoding
                            // Encode as UTF-8 bytes offset by base ID
                            var bytes = Encoding.UTF8.GetBytes(charStr);
                            foreach (var b in bytes)
                            {
                                tokens.Add(256 + b); // Offset to avoid collision with main vocab
                            }
                        }
                    }
                }

                // Check max length
                if (maxLength > 0 && tokens.Count >= maxLength - (addSpecialTokens ? 1 : 0))
                {
                    break;
                }
            }

            if (addSpecialTokens)
            {
                tokens.Add(EosTokenId);
            }

            // Apply max length truncation
            if (maxLength > 0 && tokens.Count > maxLength)
            {
                tokens = tokens.Take(maxLength).ToList();
                if (addSpecialTokens && tokens[^1] != EosTokenId)
                {
                    tokens[^1] = EosTokenId; // Ensure EOS is at the end
                }
            }

            // Apply padding if requested
            if (padding && maxLength > 0)
            {
                while (tokens.Count < maxLength)
                {
                    tokens.Add(PadTokenId);
                }
            }

            return tokens.Select(t => (long)t).ToArray();
        }

        /// <summary>
        /// Decodes token IDs back into text.
        /// </summary>
        /// <param name="tokenIds">Array of token IDs</param>
        /// <param name="skipSpecialTokens">Whether to skip special tokens in output</param>
        /// <returns>Decoded text</returns>
        public string Decode(long[] tokenIds, bool skipSpecialTokens = true)
        {
            if (tokenIds == null || tokenIds.Length == 0)
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            var byteBuffer = new List<byte>();

            foreach (var tokenId in tokenIds)
            {
                var id = (int)tokenId;

                // Handle special tokens
                if (skipSpecialTokens && (id == PadTokenId || id == BosTokenId || id == EosTokenId))
                {
                    continue;
                }

                // Flush any pending byte-level tokens
                if (byteBuffer.Count > 0 && id < 256)
                {
                    result.Append(Encoding.UTF8.GetString(byteBuffer.ToArray()));
                    byteBuffer.Clear();
                }

                // Handle byte-level encoding (offset tokens)
                if (id >= 256 && id < 512)
                {
                    byteBuffer.Add((byte)(id - 256));
                    continue;
                }

                // Handle regular tokens
                if (_reverseVocab.TryGetValue(id, out var token))
                {
                    result.Append(token);
                }
                else
                {
                    // Unknown token
                    if (!skipSpecialTokens)
                    {
                        result.Append(UnkToken);
                    }
                }
            }

            // Flush remaining byte buffer
            if (byteBuffer.Count > 0)
            {
                try
                {
                    result.Append(Encoding.UTF8.GetString(byteBuffer.ToArray()));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TokenizerService] Failed to decode byte buffer: {ex}");
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Decodes integer token IDs back into text.
        /// </summary>
        public string Decode(int[] tokenIds, bool skipSpecialTokens = true)
        {
            return Decode(tokenIds.Select(id => (long)id).ToArray(), skipSpecialTokens);
        }

        /// <summary>
        /// Encodes multiple texts into a batch of token IDs with consistent shapes.
        /// </summary>
        public long[][] EncodeBatch(
            string[] texts,
            int maxLength = 512,
            bool addSpecialTokens = true,
            bool padding = true)
        {
            var batch = new long[texts.Length][];
            
            for (var i = 0; i < texts.Length; i++)
            {
                batch[i] = Encode(texts[i], maxLength, addSpecialTokens, padding);
            }

            return batch;
        }

        /// <summary>
        /// Decodes multiple token ID sequences into texts.
        /// </summary>
        public string[] DecodeBatch(long[][] tokenIdBatch, bool skipSpecialTokens = true)
        {
            var texts = new string[tokenIdBatch.Length];
            
            for (var i = 0; i < tokenIdBatch.Length; i++)
            {
                texts[i] = Decode(tokenIdBatch[i], skipSpecialTokens);
            }

            return texts;
        }

        /// <summary>
        /// Gets the vocabulary size.
        /// </summary>
        public int VocabSize => Math.Max(_vocab.Count, 512); // Reserve space for byte-level tokens
    }
}