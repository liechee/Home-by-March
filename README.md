# Home by March - Setup and Version Control Guide

## Prerequisites

### Download and Install Unity Hub
- **Link**: [Unity Hub Download](https://unity.com/download)
- Follow the instructions on the Unity website to download and install Unity Hub on your machine.

### Download and Install Plastic SCM for Version Control
- **Link**: [Plastic SCM Download](https://www.plasticscm.com/download)
- Plastic SCM is recommended for advanced version control with Unity. Download and install the software from the link above.

## Access the "Home by March" Project

1. **Login** to Unity Hub with your account connected to Unity Teams Cloud.
2. **Locate** the "Home by March" project in the Unity Hub project list.
3. **Click** on the project to open it.

## Set Up Visual Studio Code

1. **Open** the "Home by March" project in Visual Studio Code.

## Set Up GitHub Version Control

### Initialize Git in the Project Folder
```bash
git init
```

### Install Commitizen for Well-Formatted Commits
Commitizen helps ensure that your commit messages follow a standardized format.
```bash
npm install -g commitizen
```

### Set Your Remote Origin to the Team’s Repository
```bash
git remote add origin https://github.com/XFL-Perseids/homebymarch.git
```

### Follow the Team's Version Control Practices

#### GitHub Version Control Practice
- **Commit Messages**: Ensure that your commits are well-formatted using Commitizen. To make a commit, use:
  ```bash
  git cz
  ```
  This will guide you through a series of prompts to create a standardized commit message.

- **Pull Latest Changes**: Always pull the latest changes from the remote repository before starting work to avoid conflicts:
  ```bash
  git pull origin dev
  ```

- **Frequent Commits**: Make frequent commits to document your progress and assist with collaboration.

- **Pull Requests**: Ensure that all pull requests are reviewed and approved by other members before merging.

#### Unity Version Control (UVCS) Practice
- **Access UVSC**: Navigate to the "Home by March" project in the team’s repository.

- **Switch to Development Branch**: Ensure you're working in the `dev` branch. If not, switch to it.

- **Create a Feature Branch**: For each feature you're working on, create a separate branch to manage changes and keep development organized.

- **Regular Check-Ins**: Regularly check in your updates to keep your work synchronized and reduce the risk of merge conflicts.

## Additional Notes

- **Stay Synchronized**: Regularly update your local project with the latest changes from both GitHub and UVCS to avoid conflicts.
- **Branch Naming**: Use descriptive names for your branches, such as `feature/add-step-counter` or `bugfix/fix-dungeon-crawl-bug`.
- **Documentation**: Ensure that all major features and changes are well-documented in the project’s documentation files.

```