name: Documentation Request
description: Report missing or unclear documentation
title: "[DOCS] "
labels: ["documentation"]
assignees: []

body:
  - type: textarea
    id: location
    attributes:
      label: Documentation Location
      description: Where is the documentation missing or unclear?

  - type: textarea
    id: description
    attributes:
      label: Description
      description: What should be documented or clarified?

  - type: textarea
    id: examples
    attributes:
      label: Example Code
      description: Any code examples that would help

  - type: checkboxes
    id: willingness
    attributes:
      label: Willingness to Contribute
      options:
        - label: I would be willing to update the documentation
