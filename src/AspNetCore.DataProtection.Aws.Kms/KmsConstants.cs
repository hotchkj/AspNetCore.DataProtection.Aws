// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
namespace AspNetCore.DataProtection.Aws.Kms
{
    /// <summary>
    /// Additional context supplied to all KMS operations used by AspNetCore.DataProtection.Aws.Kms
    /// </summary>
    /// <remarks>
    /// This is not sensitive data - it simply prevents arbitrary use of a KMS master key to decrypt
    /// the XML without this context supplied.
    /// 
    /// It is expected that clients will supply additional context such as their application name.
    /// 
    /// These strings should remain unchanged, rather than reflect any code or namespace - they're
    /// only written as a namespace for easy disambiguation.
    /// </remarks>
    public static class KmsConstants
    {
        /// <summary>
        /// KMS context key provided by default. Value is <see cref="DefaultEncryptionContextValue"/>.
        /// </summary>
        public const string DefaultEncryptionContextKey = "AspNetCore.DataProtection.Aws.Kms.Xml";
        /// <summary>
        /// KMS context value provided by default. Key is <see cref="DefaultEncryptionContextValue"/>.
        /// </summary>
        public const string DefaultEncryptionContextValue = "b7b7f5af-d3c3-436d-8792-87dfd65e1cd4";
        /// <summary>
        /// KMS context key used when setting application ID.
        /// </summary>
        public const string ApplicationEncryptionContextKey = "AspNetCore.DataProtection.Aws.Kms.Xml.ApplicationName";
    }
}
