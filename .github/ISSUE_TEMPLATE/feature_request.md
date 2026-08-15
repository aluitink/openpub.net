name: Feature Request
description: Suggest a new feature or improvement
title: "[FEATURE] "
labels: ["enhancement"]
assignees: []

body:
  - type: textarea
    id: problem
    attributes:
      label: Problem Statement
      description: A clear description of the problem this feature would solve
    validations:
      required: true

  - type: textarea
    id: solution
    attributes:
      label: Proposed Solution
      description: A clear description of how you'd like to see this feature implemented

  - type: textarea
    id: alternatives
    attributes:
      label: Alternative Approaches
      description: Any alternative solutions you've considered

  - type: textarea
    id: context
    attributes:
      label: Additional Context
      description: Any other context, screenshots, or files about the feature request

  - type: checkboxes
    id: willingness
    attributes:
      label: Willingness to Contribute
      options:
        - label: I would be willing to implement this feature
