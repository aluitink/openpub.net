name: Security Vulnerability
description: Report a security vulnerability
title: "[SECURITY] "
labels: ["security"]
assignees: []

body:
  - type: textarea
    id: vulnerability
    attributes:
      label: Vulnerability Description
      description: A detailed description of the security vulnerability
    validations:
      required: true

  - type: textarea
    id: impact
    attributes:
      label: Impact Assessment
      description: What is the potential impact of this vulnerability?

  - type: textarea
    id: reproduction
    attributes:
      label: Reproduction Steps
      description: Steps to reproduce the vulnerability

  - type: input
    id: affected-version
    attributes:
      label: Affected Version
      description: Which versions are affected?

  - type: textarea
    id: mitigation
    attributes:
      label: Mitigation Steps
      description: Any steps to mitigate the vulnerability before patching

  - type: markdown
    attributes:
      value: |
        **Important**: For security-sensitive issues, please avoid public disclosure until we can assess and remediate.
