using System;
using System.Configuration;

namespace Ytl.VerificationUtils
{
    public class VerificationEnvironment
    {
        public static string DigabiBaseUrl = ConfigurationManager.AppSettings["DigabiBaseUrl"];

        public static string DirectoryPath { get; set; }
    }
}