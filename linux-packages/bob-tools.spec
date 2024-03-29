Name: bob-tools
Summary: Various tools for interacting with bob
License: MIT
URL: https://github.com/qoollo/bob-tools
Version: current_version
Release: release_number
Source0: %{name}-%{version}.tar.gz
Group: Development/Tools
BuildArch: x86_64
Requires: rsync

%global debug_package %{nil}
%global __os_install_post %{nil}

%description
Various tools for interacting with bob:
Records calculator. Tool for counting all unique records and replicas in bob.
Old partitions remover. Tool for removing all partitions in cluster older than provided date.
Disk status analyzer. Tool for copying aliens.
BobAliensRecovery. Tool for recovering aliens from nodes.
Disks monitoring. Tool for discovery, formatting and mounting of new disks.

%prep
%setup -q

%build

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}/usr/bin/
mkdir -p %{buildroot}/etc/DisksMonitoring/
mkdir -p %{buildroot}/etc/systemd/system/
mkdir -p %{buildroot}/lib/systemd/system/

cp ClusterModifier %{buildroot}/usr/bin/
cp DisksMonitoring %{buildroot}/usr/bin/
cp BobAliensRecovery %{buildroot}/usr/bin/
cp OldPartitionsRemover %{buildroot}/usr/bin/
cp RecordsCalculator %{buildroot}/usr/bin/
cp publish/BobTools.sh %{buildroot}/usr/bin/bobtools
cp linux-packages/DisksMonitoring.service %{buildroot}/etc/systemd/system/
cp linux-packages/DisksMonitoring.service %{buildroot}/lib/systemd/system/

%clean
rm -rf %{buildroot}

%files
%attr(0755, root, root) /usr/bin/ClusterModifier
%attr(0755, root, root) /usr/bin/DisksMonitoring
%attr(0755, root, root) /usr/bin/BobAliensRecovery
%attr(0755, root, root) /usr/bin/OldPartitionsRemover
%attr(0755, root, root) /usr/bin/RecordsCalculator
%attr(0755, root, root) /usr/bin/bobtools
/etc/systemd/system/DisksMonitoring.service
/lib/systemd/system/DisksMonitoring.service
%dir /etc/DisksMonitoring/
