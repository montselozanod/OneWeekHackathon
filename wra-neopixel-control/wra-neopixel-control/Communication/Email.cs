using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace SendEmail
{
    class Email
    {
        // Tested with gmail not outlook, should still work
        private static string smtpAddress = "smtp-mail.outlook.com";
        private static int portNumber = 587;
        private static bool enableSSL = true;

        /// <summary>
        /// Send an email. Example:
        /// Email.SendEmail("toyou@hotmail.com", "This is a test body", "Test subject", "fromme@gmail.com",
        ///    "fromme@gmail.com", "user@gmail.com", "password");
        /// </summary>
        /// <param name="to">Message to address</param>
        /// <param name="body">Text of message to send</param>
        /// <param name="subject">Subject line of message</param>
        /// <param name="fromAddress">Message from address</param>
        /// <param name="fromDisplay">Display name for "message from address"</param>
        /// <param name="credentialUser">User whose credentials are used for message send</param>
        /// <param name="credentialPassword">User password used for message send</param>
        /// <param name="attachments">Optional attachments for message</param>
        public static void SendEmail(string to,
                                     string body,
                                     string subject,
                                     string fromAddress,
                                     string fromDisplay,
                                     string credentialUser,
                                     string credentialPassword)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.Body = body;
                mail.IsBodyHtml = true;
                mail.To.Add(new MailAddress(to));
                mail.From = new MailAddress(fromAddress, fromDisplay, Encoding.UTF8);
                mail.Subject = subject;
                mail.SubjectEncoding = Encoding.UTF8;
                mail.Priority = MailPriority.Normal;

                using (SmtpClient smtp = new SmtpClient(smtpAddress, portNumber))
                {
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(credentialUser, credentialPassword);
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.EnableSsl = enableSSL;
                    smtp.Send(mail);
                }
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder(1024);
                sb.Append("\nTo:" + to);
                sb.Append("\nbody:" + body);
                sb.Append("\nsubject:" + subject);
                sb.Append("\nfromAddress:" + fromAddress);
                sb.Append("\nfromDisplay:" + fromDisplay);
                sb.Append("\ncredentialUser:" + credentialUser);
                sb.Append("\ncredentialPasswordto:" + credentialPassword);
                Trace.TraceError(sb.ToString(), ex.ToString());
            }
        }
    }
}
