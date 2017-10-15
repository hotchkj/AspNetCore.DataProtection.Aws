// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
namespace AspNetCore.DataProtection.Aws.TestCoreWebApp.Models
{
    public class ErrorViewModel
    {
        public string RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}