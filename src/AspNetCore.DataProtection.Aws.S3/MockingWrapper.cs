// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;

namespace AspNetCore.DataProtection.Aws.S3
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
    public class MockingWrapper : IMockingWrapper
    {
        /// <inheritdoc/>
        public Guid GetNewGuid()
        {
            return Guid.NewGuid();
        }
    }
}
