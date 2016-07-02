// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;

namespace AspNetCore.DataProtection.Aws.S3
{
    public interface IMockingWrapper
    {
        Guid GetNewGuid();
    }

    public class MockingWrapper : IMockingWrapper
    {
        public Guid GetNewGuid()
        {
            return Guid.NewGuid();
        }
    }
}
