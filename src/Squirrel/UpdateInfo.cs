﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;
using Squirrel.SimpleSplat;

namespace Squirrel
{
    /// <summary>
    /// Holds information about the current version and pending updates, such as how many there are, and access to release notes.
    /// </summary>
    [DataContract]
    public class UpdateInfo : IEnableLogger
    {
        /// <summary>
        /// The currently executing version of the application, or null if not currently installed.
        /// </summary>
        [DataMember] public ReleaseEntry LatestLocalReleaseEntry { get; protected set; }

        /// <summary>
        /// The same as <see cref="LatestLocalReleaseEntry"/> if there are no updates available, otherwise
        /// this will be the version that we are updating to.
        /// </summary>
        [DataMember] public ReleaseEntry FutureReleaseEntry { get; protected set; }

        /// <summary>
        /// The list of versions between the <see cref="LatestLocalReleaseEntry"/> and <see cref="FutureReleaseEntry"/>.
        /// These will all be applied in order.
        /// </summary>
        [DataMember] public List<ReleaseEntry> ReleasesToApply { get; protected set; }

        /// <summary>
        /// Path to folder containing local/downloaded packages
        /// </summary>
        [IgnoreDataMember]
        public string PackageDirectory { get; protected set; }

        /// <summary>
        /// Create a new instance of <see cref="UpdateInfo"/>
        /// </summary>
        protected UpdateInfo(ReleaseEntry currentlyInstalledVersion, IEnumerable<ReleaseEntry> releasesToApply, string localPackageDirectory)
        {
            // NB: When bootstrapping, CurrentlyInstalledVersion is null!
            LatestLocalReleaseEntry = currentlyInstalledVersion;
            ReleasesToApply = (releasesToApply ?? Enumerable.Empty<ReleaseEntry>()).ToList();
            FutureReleaseEntry = ReleasesToApply.Any() ?
                ReleasesToApply.MaxBy(x => x.Version).FirstOrDefault() :
                LatestLocalReleaseEntry;

            this.PackageDirectory = localPackageDirectory;
        }

        /// <summary>
        /// Retrieves all the release notes for pending packages (ie. <see cref="ReleasesToApply"/>)
        /// </summary>
        public Dictionary<ReleaseEntry, string> FetchReleaseNotes(ReleaseNotesFormat format)
        {
            return ReleasesToApply
                .SelectMany(x => {
                    try {
                        var releaseNotes = x.GetReleaseNotes(PackageDirectory, format);
                        return EnumerableExtensions.Return(Tuple.Create(x, releaseNotes));
                    } catch (Exception ex) {
                        this.Log().WarnException("Couldn't get release notes for:" + x.Filename, ex);
                        return Enumerable.Empty<Tuple<ReleaseEntry, string>>();
                    }
                })
                .ToDictionary(k => k.Item1, v => v.Item2);
        }

        /// <summary>
        /// Create a new <see cref="UpdateInfo"/> from a current release and a list of future releases
        /// yet to be installed.
        /// </summary>
        /// <exception cref="Exception">When availableReleases is null or empty</exception>
        public static UpdateInfo Create(ReleaseEntry currentVersion, IEnumerable<ReleaseEntry> availableReleases, string localPackageDirectory)
        {
            Contract.Requires(availableReleases != null);
            Contract.Requires(!String.IsNullOrEmpty(localPackageDirectory));

            var latestFull = availableReleases.MaxBy(x => x.Version).FirstOrDefault(x => !x.IsDelta);
            if (latestFull == null) {
                throw new Exception("There should always be at least one full release");
            }

            if (currentVersion == null) {
                return new UpdateInfo(null, new[] { latestFull }, localPackageDirectory);
            }

            if (currentVersion.Version >= latestFull.Version) {
                return new UpdateInfo(currentVersion, Enumerable.Empty<ReleaseEntry>(), localPackageDirectory);
            }

            // KTK TODO: move to new function near Utility.FindLatestFullVersion.
            var newerThanUs = availableReleases
                .Where(x => x.Version > currentVersion.Version)
                .OrderBy(v => v.Version)
                .ToArray();

            var deltasSize = newerThanUs.Where(x => x.IsDelta).Sum(x => x.Filesize);
            var deltasCount = newerThanUs.Count(x => x.IsDelta);

            // delta's are cheap to download, but really expensive to apply.
            // full packages are more expensive to download, but really cheap to apply.
            // this tries to find a good balance of both. we will go for the full if
            // there are too many delta's or if their file size is too large.
            return (deltasSize > 0 && (deltasSize * 10) < latestFull.Filesize && deltasCount <= 10) ?
                new UpdateInfo(currentVersion, newerThanUs.Where(x => x.IsDelta).ToArray(), localPackageDirectory) :
                new UpdateInfo(currentVersion, new[] { latestFull }, localPackageDirectory);
        }
    }
}
