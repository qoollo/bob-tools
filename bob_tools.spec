Name: bob_tools
Summary: Various tools for interacting with bob
License: MIT
URL: https://github.com/qoollo/bob_tools
Version: 1.0.1.0
Release: qwerty123
Source0: %{name}-%{version}.tar.gz
Group: Development/Tools
BuildArch: x86_64

%global debug_package %{nil}
%global __os_install_post %{nil}

%description
Various tools for interacting with bob:
Records calculator.Tool for counting all unique records and replicas in bob.
Old partitions remover. Tool for removing all partitions in cluster older than provided date.
Disk status analyzer. Tool for copying aliens.
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
cp DiskStatusAnalyzer %{buildroot}/usr/bin/
cp OldPartitionsRemover %{buildroot}/usr/bin/
cp RecordsCalculator %{buildroot}/usr/bin/
cp DisksMonitoring.service %{buildroot}/etc/systemd/system/
cp DisksMonitoring.service %{buildroot}/lib/systemd/system/

%clean
rm -rf %{buildroot}

%files
%attr(0755, root, root) /usr/bin/ClusterModifier
%attr(0755, root, root) /usr/bin/DisksMonitoring
%attr(0755, root, root) /usr/bin/DiskStatusAnalyzer
%attr(0755, root, root) /usr/bin/OldPartitionsRemover
%attr(0755, root, root) /usr/bin/RecordsCalculator
/etc/systemd/system/DisksMonitoring.service
/lib/systemd/system/DisksMonitoring.service
%dir /etc/DisksMonitoring/