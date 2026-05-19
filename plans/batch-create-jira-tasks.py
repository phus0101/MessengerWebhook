"""
Batch create Jira tasks from CSV without parent linking.
Tasks will be created independently and can be organized manually in Jira UI.
"""
import csv
import json
import time

CLOUD_ID = "e5a49acb-2043-4ef2-9bd9-3fdacc6615f0"
PROJECT_KEY = "AG247"
CSV_FILE = "plans/jira-import-ag247-missing-tasks.csv"

def read_tasks_only():
    """Read only Task type issues from CSV."""
    tasks = []
    with open(CSV_FILE, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row['Issue Type'] == 'Task':
                tasks.append(row)
    return tasks

def generate_task_batches(tasks, batch_size=10):
    """Split tasks into batches for processing."""
    batches = []
    for i in range(0, len(tasks), batch_size):
        batches.append(tasks[i:i + batch_size])
    return batches

def format_task_for_mcp(task):
    """Format task data for MCP createJiraIssue call."""
    cmd = {
        "cloudId": CLOUD_ID,
        "projectKey": PROJECT_KEY,
        "issueTypeName": "Task",
        "summary": task['Summary'],
        "description": task['Description'],
        "contentFormat": "markdown"
    }

    # Add labels if present
    if task.get('Labels'):
        labels = [l.strip() for l in task['Labels'].split(',')]
        cmd["additional_fields"] = {"labels": labels}

    return cmd

def main():
    print("Đọc tasks từ CSV...")
    tasks = read_tasks_only()
    print(f"Tổng số tasks cần tạo: {len(tasks)}")

    # Group by parent for reporting
    by_parent = {}
    for task in tasks:
        parent = task.get('Parent', 'No Parent').strip() or 'No Parent'
        if parent not in by_parent:
            by_parent[parent] = []
        by_parent[parent].append(task)

    print(f"\nPhân bổ tasks theo parent:")
    for parent, parent_tasks in sorted(by_parent.items()):
        print(f"  {parent}: {len(parent_tasks)} tasks")

    # Generate batches
    batches = generate_task_batches(tasks, batch_size=10)
    print(f"\nChia thành {len(batches)} batches (10 tasks/batch)")

    # Save commands for manual execution
    all_commands = []
    for i, batch in enumerate(batches, 1):
        batch_commands = []
        print(f"\n=== BATCH {i}/{len(batches)} ===")
        for task in batch:
            cmd = format_task_for_mcp(task)
            batch_commands.append(cmd)
            parent = task.get('Parent', 'No Parent').strip() or 'No Parent'
            print(f"  - {task['Summary']} (Parent: {parent})")
        all_commands.append(batch_commands)

    # Save to JSON
    output_file = "plans/jira-task-batches.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(all_commands, f, indent=2, ensure_ascii=False)

    print(f"\n✓ Đã lưu {len(batches)} batches vào: {output_file}")
    print(f"\nTổng cộng: {len(tasks)} tasks sẽ được tạo")
    print("\nLưu ý: Tasks sẽ được tạo độc lập, không có parent link.")
    print("Bạn có thể organize chúng trong Jira UI sau khi tạo xong.")

if __name__ == "__main__":
    main()
