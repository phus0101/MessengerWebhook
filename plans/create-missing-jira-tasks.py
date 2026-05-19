"""
Script to create missing Jira tasks from CSV file using MCP Atlassian plugin.
This script reads the missing tasks CSV and creates them in batches.
"""
import csv
import json
import sys

# Configuration
CLOUD_ID = "e5a49acb-2043-4ef2-9bd9-3fdacc6615f0"
PROJECT_KEY = "AG247"
CSV_FILE = "plans/jira-import-ag247-missing-tasks.csv"

def read_missing_tasks():
    """Read missing tasks from CSV file."""
    tasks = []
    with open(CSV_FILE, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            tasks.append(row)
    return tasks

def group_tasks_by_parent(tasks):
    """Group tasks by their parent Story/Epic."""
    grouped = {}
    stories = []
    orphan_tasks = []

    for task in tasks:
        issue_type = task['Issue Type']
        parent = task.get('Parent', '').strip()

        if issue_type == 'Story':
            stories.append(task)
        elif issue_type == 'Task':
            if parent:
                if parent not in grouped:
                    grouped[parent] = []
                grouped[parent].append(task)
            else:
                orphan_tasks.append(task)

    return stories, grouped, orphan_tasks

def generate_mcp_commands(tasks):
    """Generate MCP command JSON for creating Jira issues."""
    stories, task_groups, orphan_tasks = group_tasks_by_parent(tasks)

    commands = []

    # First, create all Stories
    print(f"\n=== STORIES TO CREATE ({len(stories)}) ===")
    for story in stories:
        cmd = {
            "tool": "mcp__plugin_atlassian_atlassian__createJiraIssue",
            "params": {
                "cloudId": CLOUD_ID,
                "projectKey": PROJECT_KEY,
                "issueTypeName": "Story",
                "summary": story['Summary'],
                "description": story['Description'],
                "contentFormat": "markdown"
            }
        }

        # Add labels if present
        if story.get('Labels'):
            labels = [l.strip() for l in story['Labels'].split(',')]
            cmd["params"]["additional_fields"] = {"labels": labels}

        commands.append(cmd)
        print(f"  - {story['Summary']}")

    # Then, create Tasks grouped by parent
    print(f"\n=== TASKS GROUPED BY PARENT ===")
    for parent_name, tasks in task_groups.items():
        print(f"\n  Parent: {parent_name} ({len(tasks)} tasks)")
        for task in tasks:
            print(f"    - {task['Summary']}")
            # Note: We'll need to get parent issue key after creating stories
            # For now, just prepare the command structure
            cmd = {
                "tool": "mcp__plugin_atlassian_atlassian__createJiraIssue",
                "params": {
                    "cloudId": CLOUD_ID,
                    "projectKey": PROJECT_KEY,
                    "issueTypeName": "Task",
                    "summary": task['Summary'],
                    "description": task['Description'],
                    "contentFormat": "markdown"
                },
                "parent_name": parent_name  # We'll use this to link later
            }

            if task.get('Labels'):
                labels = [l.strip() for l in task['Labels'].split(',')]
                if "additional_fields" not in cmd["params"]:
                    cmd["params"]["additional_fields"] = {}
                cmd["params"]["additional_fields"]["labels"] = labels

            commands.append(cmd)

    # Orphan tasks (no parent)
    if orphan_tasks:
        print(f"\n=== ORPHAN TASKS ({len(orphan_tasks)}) ===")
        for task in orphan_tasks:
            print(f"  - {task['Summary']}")

    return commands

def main():
    print("Reading missing tasks from CSV...")
    tasks = read_missing_tasks()
    print(f"Total tasks to create: {len(tasks)}")

    print("\nGenerating MCP commands...")
    commands = generate_mcp_commands(tasks)

    # Save commands to JSON file for reference
    output_file = "plans/jira-create-commands.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(commands, f, indent=2, ensure_ascii=False)

    print(f"\n✓ Commands saved to: {output_file}")
    print(f"\nTotal commands to execute: {len(commands)}")
    print("\nNote: Due to parent-child relationships, tasks must be created in phases:")
    print("  Phase 1: Create all Stories first")
    print("  Phase 2: Get Story keys from Jira")
    print("  Phase 3: Create Tasks with parent links")

if __name__ == "__main__":
    main()
