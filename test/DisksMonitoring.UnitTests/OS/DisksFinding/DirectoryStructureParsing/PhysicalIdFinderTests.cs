using System.Collections.Generic;
using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using AutoFixture.NUnit3;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace DisksMonitoring.UnitTests.OS.DisksFinding.DirectoryStructureParsing
{
    public class PhysicalIdFinderTests
    {
        [Test, AD]
        public void Find_WithEmptyDir_ReturnsEmptyDictionary(
            IFileSystemAccessor fileSystemAccessor,
            PhysicalIdFinder sut)
        {
            A.CallTo(() => fileSystemAccessor.GetFilenames("/dev/disk/by-path"))
                .Returns(new List<string>());

            var result = sut.Find();

            result.Should().BeEmpty();
        }

        private class ADAttribute : AutoDataAttribute
        {
            public ADAttribute() : base(() => new Fixture().Customize(new AutoFakeItEasyCustomization())) { }
        }
    }
}