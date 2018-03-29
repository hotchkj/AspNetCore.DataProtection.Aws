// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Diagnostics;
using System.Linq;
using AspNetCore.DataProtection.Aws.TestCoreWebApp.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.DataProtection.Aws.TestCoreWebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDataProtectionProvider protectionProvider;

        public HomeController(IDataProtectionProvider protectionProvider)
        {
            this.protectionProvider = protectionProvider ?? throw new ArgumentNullException(nameof(protectionProvider));
        }

        public IActionResult Index()
        {
            var protector = protectionProvider.CreateProtector("testing");
            var plaintext = new byte[] { 1, 2, 3, 4, 5, 6 };
            var ciphertext = protector.Protect(plaintext);
            var resultText = protector.Unprotect(ciphertext);

            ViewData["Message"] = resultText.SequenceEqual(plaintext) ? "Protection round trip succeeded" : "Protection round trip mismatch";

            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
