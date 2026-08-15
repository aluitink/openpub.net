name: Question
description: Ask a question about ActivityPub.NET
title: "[QUESTION] "
labels: ["question"]
assignees: []

body:
  - type: textarea
    id: question
    attributes:
      label: Question
      description: Your question about ActivityPub.NET
    validations:
      required: true

  - type: textarea
    id: context
    attributes:
      label: Context
      description: What are you trying to achieve?

  - type: textarea
    id: research
    attributes:
      label: What I've Tried
      description: What have you already tried and researched?
