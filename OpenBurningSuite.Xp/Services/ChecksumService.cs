// Copyright (c) 2026 SvenGDK
// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenBurningSuite.Xp.Services
{
    internal static class ChecksumService
    {
        public static string ComputeFileHash(string filePath, string algorithmName)
        {
            using (HashAlgorithm algorithm = CreateAlgorithm(algorithmName))
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);

                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));

                return builder.ToString();
            }
        }

        private static HashAlgorithm CreateAlgorithm(string algorithmName)
        {
            switch ((algorithmName ?? string.Empty).ToUpperInvariant())
            {
                case "MD5":
                    return MD5.Create();
                case "SHA1":
                    return SHA1.Create();
                case "SHA512":
                    return SHA512.Create();
                default:
                    return SHA256.Create();
            }
        }
    }
}
