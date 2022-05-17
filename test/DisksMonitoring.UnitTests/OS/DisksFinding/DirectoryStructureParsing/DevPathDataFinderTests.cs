using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using AutoFixture.NUnit3;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing;
using DisksMonitoring.OS.DisksFinding.DirectoryStructureParsing.FileSystemAccessors;
using DisksMonitoring.OS.DisksFinding.Entities;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace DisksMonitoring.UnitTests.OS.DisksFinding.DirectoryStructureParsing
{
    public class DevPathDataFinderTests
    {
        const string Dir = "/";

        [Test, AD]
        public void Find_WithEmptyDir_ReturnsEmptyDictionary(
            IFileSystemAccessor fileSystemAccessor,
            DevPathDataFinder sut)
        {
            SetupFiles(fileSystemAccessor);

            var result = sut.Find(Dir, s => s);

            result.Should().BeEmpty();
        }

        [Test, AD]
        public void Find_WithAtaFile_ReturnsPhysicalId(
            IFileSystemAccessor fileSystemAccessor,
            DevPathDataFinder sut)
        {
            SetupFiles(fileSystemAccessor, ("pci-0000:00:0d.0-ata-1", "sda"));

            var result = sut.Find(Dir, s => s);

            result.Should().ContainSingle();
        }

        [Test, AD]
        public void Find_WithAtaFile_ReturnsCorrectPhysicalId(
            IFileSystemAccessor fileSystemAccessor,
            DevPathDataFinder sut)
        {
            SetupFiles(fileSystemAccessor, ("pci-0000:00:0d.0-ata-1", "sda"));

            var result = sut.Find(Dir, s => s);

            result.Values.Single().Should().Be("pci-0000:00:0d.0-ata-1");
        }

        private void SetupFiles(IFileSystemAccessor fileSystemAccessor, params (string filename, string devFilename)[] files)
        {
            var filenames = files.Select(t => t.filename).ToList();
            A.CallTo(() => fileSystemAccessor.GetFilenames(Dir)).Returns(filenames.ToList());
            foreach (var (filename, dev) in files)
                A.CallTo(() => fileSystemAccessor.FindDevDiskPathTargetFile(filename)).Returns("/dev/" + dev);
        }

        private class ADAttribute : AutoDataAttribute
        {
            public ADAttribute() : base(() =>
            {
                var fixture = new Fixture().Customize(new AutoFakeItEasyCustomization());
                fixture.Freeze<IFileSystemAccessor>();
                return fixture;
            })
            { }
        }
    }
}