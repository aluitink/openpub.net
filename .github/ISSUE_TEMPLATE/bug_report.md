name: Bug Report
description: Report a bug or issue
title: "[BUG] "
labels: ["bug"]
assignees: []

body:
  - type: textarea
    id: description
    attributes:
      label: Description
      description: A clear description of what the bug is
    validations:
      required: true

  - type: textarea
    id: reproduction
    attributes:
      label: Steps to Reproduce
      description: Steps to reproduce the behavior
      placeholder: |
        1. Go to '...'
        2. Click on '...'
        3. Scroll down to '...'
        4. See error

  - type: textarea
    id: expected
    attributes:
      label: Expected Behavior
      description: What you expected to happen

  - type: textarea
    id: actual
    attributes:
      label: Actual Behavior
      description: What actually happened

  - type: input
    id: version
    attributes:
      label: Version
      description: What version of ActivityPub.NET are you using?
      placeholder: "1.0.0"

  - type: input
    id: dotnet-version
    attributes:
      label: .NET Version
      placeholder: "10.0.x"

  - type: dropdown
    id: os
    attributes:
      label: Operating System
      options:
        - Windows
        - macOS
        - Linux

  - type: textarea
    id: logs
    attributes:
      label: Relevant Log Output
      description: Please copy and paste any relevant log output
      render: shell
