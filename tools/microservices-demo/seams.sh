#!/bin/bash
# Where could a network boundary go? Print what each project references,
# and the interfaces the application layer owns.
cd "$(dirname "$0")/../.."
echo "Project references:"
for p in Api Infrastructure Application Domain; do
  refs=$(grep ProjectReference src/TaskFlow.$p/TaskFlow.$p.csproj | grep -o 'TaskFlow\.[A-Za-z]*\.csproj' | sed 's/\.csproj//' | tr '\n' ' ')
  echo "  TaskFlow.$p -> ${refs:-nothing}"
done
echo
echo "Interfaces owned by the application layer:"
grep -h "^public interface" src/TaskFlow.Application/*.cs | sed 's/^/  /'
