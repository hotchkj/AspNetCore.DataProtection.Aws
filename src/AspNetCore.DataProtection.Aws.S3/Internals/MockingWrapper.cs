// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Diagnostics.CodeAnalysis;

namespace AspNetCore.DataProtection.Aws.S3.Internals
{
    /// <summary>
    /// Provides mockable interfaces for NET Framework entries.
    /// </summary>
    public interface IMockingWrapper
    {
        /// <summary>
        /// Wraps <see cref="Guid.NewGuid()"/>.
        /// </summary>
        /// <returns>New GUID.</returns>
        Guid GetNewGuid();
    }

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage]
    public class MockingWrapper : IMockingWrapper
    {
        /// <inheritdoc/>
        public Guid GetNewGuid()
        {
            return Guid.NewGuid();
        }
    }
}
