using System;
using System.Text.RegularExpressions;
using BobToolsCli;
using ByteSizeLib;
using CommandLine;
using OldPartitionsRemover.Entities;

namespace OldPartitionsRemover.BySpaceRemoving
{
    public enum ThresholdType
    {
        Free,
        Occupied
    }
}
