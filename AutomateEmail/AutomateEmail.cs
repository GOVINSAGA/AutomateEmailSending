using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

class AutomateEmail
{
    static void Main(string[] args)
    {
        // 1. Load configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var myEmail = config["Email:Address"];
        var myAppPassword = config["Email:AppPassword"];
        var dailyLimit = int.Parse(config["Email:DailyLimit"] ?? "50");
        var subject = config["Email:Subject"];
        var templateBody = config["Email:TemplateBody"];

        if (string.IsNullOrEmpty(myEmail) || string.IsNullOrEmpty(myAppPassword))
        {
            Console.WriteLine("Error: Email configuration is missing in appsettings.json");
            return;
        }

        // Get the directory where the .exe is running
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var resumePath = Path.Combine(baseDir, config["Files:ResumePath"] ?? "govind_cv.pdf");
        var emailListPath = Path.Combine(baseDir, config["Files:EmailListPath"] ?? "emails.txt");
        var emailSentListPath = Path.Combine(baseDir, config["Files:SentListPath"] ?? "emailSentList.txt");

        // Validate files exist
        if (!File.Exists(emailListPath))
        {
            Console.WriteLine($"No emails.txt file found at {emailListPath}");
            return;
        }

        if (!File.Exists(resumePath))
        {
            Console.WriteLine($"No resume file found at {resumePath}");
            return;
        }

        int sentCount = 0;

        // 2. Read the remaining HR emails
        var allEmails = File.ReadAllLines(emailListPath)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToList();

        var remainingEmails = new List<string>(allEmails);
        var newlySentEmails = new List<string>();

        if (allEmails.Count == 0)
        {
            Console.WriteLine("Your email list is empty. Job done!");
            return;
        }

        using var client = new SmtpClient();

        try
        {
            // Connect to Gmail's SMTP server
            client.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            client.Authenticate(myEmail, myAppPassword);

            // 3. Loop through the list and send up to the daily limit
            foreach (var hrEmail in allEmails)
            {
                if (sentCount >= dailyLimit)
                {
                    Console.WriteLine($"\nReached the daily safety limit of {dailyLimit} emails.");
                    break;
                }

                var trimmedEmail = hrEmail.Trim();

                try
                {
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress("Govind Sagar", myEmail));
                    message.To.Add(new MailboxAddress("", trimmedEmail));
                    message.Subject = subject;

                    // Attach the resume and set the template
                    var builder = new BodyBuilder { TextBody = templateBody };
                    builder.Attachments.Add(resumePath);
                    message.Body = builder.ToMessageBody();

                    // Send the email
                    client.Send(message);
                    Console.WriteLine($"[{sentCount + 1}/{dailyLimit}] Successfully sent to: {trimmedEmail}");

                    // Record success and remove from the remaining list
                    newlySentEmails.Add(trimmedEmail);
                    remainingEmails.Remove(trimmedEmail);
                    sentCount++;

                    // Add a small delay to avoid triggering spam filters
                    Thread.Sleep(3000);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Failed to send to {trimmedEmail}: {emailEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"A connection error occurred: {ex.Message}");
        }
        finally
        {
            client.Disconnect(true);

            // 4. Update both text files
            if (newlySentEmails.Count > 0)
            {
                File.AppendAllLines(emailSentListPath, newlySentEmails);
                File.WriteAllLines(emailListPath, remainingEmails);

                Console.WriteLine($"\nMoved {newlySentEmails.Count} emails to emailSentList.txt.");
                Console.WriteLine($"{remainingEmails.Count} emails left for the coming days.");
            }

            Console.WriteLine("Daily email batch complete.");
        }
    }
}