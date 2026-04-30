import subprocess
import re
from collections import defaultdict
from typing import List, Dict, Set, Tuple
import json

def run_git_command(cmd: List[str]) -> str:
    """Run git command and return output"""
    result = subprocess.run(cmd, capture_output=True, text=True)
    return result.stdout.strip()

def get_new_files(tag: str) -> List[str]:
    """Get all files added since tag"""
    cmd = ['git', 'diff', '--diff-filter=A', '--name-only', tag, 'HEAD']
    output = run_git_command(cmd)
    return [f for f in output.split('\n') if f]

def get_modified_files(tag: str) -> List[str]:
    """Get all files modified since tag"""
    cmd = ['git', 'diff', '--diff-filter=M', '--name-only', tag, 'HEAD']
    output = run_git_command(cmd)
    return [f for f in output.split('\n') if f]

def get_commits_for_file(file_path: str, tag: str) -> List[Dict]:
    """Get commits that modified a specific file since tag"""
    cmd = ['git', 'log', '--oneline', '--format=%H|%s', f'{tag}..HEAD', '--', file_path]
    output = run_git_command(cmd)
    
    commits = []
    for line in output.split('\n'):
        if '|' in line:
            commit_hash, subject = line.split('|', 1)
            commits.append({
                'hash': commit_hash[:8],
                'full_hash': commit_hash,
                'subject': subject
            })
    return commits

def get_pr_info(commit_hash: str) -> Tuple[str, str]:
    """Extract PR number and title from commit message"""
    # Get full commit message
    cmd = ['git', 'log', '--format=%B', '-n', '1', commit_hash]
    message = run_git_command(cmd)
    
    # Look for PR pattern (#123 or pull request #123)
    pr_match = re.search(r'\(#(\d+)\)', message) or re.search(r'pull request #(\d+)', message, re.I)
    
    if pr_match:
        pr_num = pr_match.group(1)
        # Get PR title (usually first line of commit message without PR number)
        title = message.split('\n')[0]
        title = re.sub(r'\(\#\d+\)', '', title).strip()
        return pr_num, title
    
    return None, None

def generate_changelog(tag: str):
    """Generate complete changelog"""
    print(f"Analyzing changes since tag: {tag}\n")
    print("=" * 80)
    
    # 1. New files
    print("\nNEW FILES ADDED:")
    print("-" * 40)
    new_files = get_new_files(tag)
    if new_files:
        for file in sorted(new_files):
            print(f"  {file}")
        print(f"\n  Total: {len(new_files)} new files")
    else:
        print("  No new files found")
    
    # 2. Modified files grouped by commits
    print("\n\nMODIFIED FILES (with commit context):")
    print("-" * 80)
    
    modified_files = get_modified_files(tag)
    
    if not modified_files:
        print("  No modified files found")
        return
    
    # Group commits by file and collect commit info
    file_commits = defaultdict(list)
    all_commits_info = {}
    
    for file_path in sorted(modified_files):
        commits = get_commits_for_file(file_path, tag)
        
        if commits:
            for commit in commits:
                # Get PR info if not already fetched
                if commit['full_hash'] not in all_commits_info:
                    pr_num, pr_title = get_pr_info(commit['full_hash'])
                    all_commits_info[commit['full_hash']] = {
                        'hash': commit['hash'],
                        'subject': commit['subject'],
                        'pr_num': pr_num,
                        'pr_title': pr_title
                    }
                
                file_commits[file_path].append(commit['full_hash'])
    
    # Display results
    for file_path in sorted(modified_files):
        print(f"\n  {file_path}")
        
        if file_path in file_commits:
            unique_commits = list(dict.fromkeys(file_commits[file_path]))  # Remove duplicates
            
            for commit_hash in unique_commits:
                info = all_commits_info[commit_hash]
                print(f"     ├─ Commit: {info['hash']}")
                print(f"     │  Message: {info['subject']}")
                if info['pr_num']:
                    print(f"     │  PR: #{info['pr_num']} - {info['pr_title']}")
                print(f"     │")
        else:
            print(f"     └─ No commits found (file may have been renamed/changed)")

def generate_markdown_changelog(tag: str, output_file: str = "gitLogChanges.md"):
    """Generate markdown formatted changelog"""
    import datetime
    
    new_files = get_new_files(tag)
    modified_files = get_modified_files(tag)
    
    content = []
    content.append(f"# Changelog - {tag} to HEAD")
    content.append(f"\n*Generated on {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}*")
    
    # New files section
    content.append(f"\n## New Files ({len(new_files)})")
    if new_files:
        for file in sorted(new_files):
            content.append(f"- `{file}`")
    else:
        content.append("- No new files")
    
    # Modified files section
    content.append(f"\n## Modified Files ({len(modified_files)})")
    
    if modified_files:
        for file_path in sorted(modified_files):
            content.append(f"\n### `{file_path}`")
            commits = get_commits_for_file(file_path, tag)
            
            if commits:
                for commit in commits[:5]:  # Limit to last 5 commits per file
                    pr_num, pr_title = get_pr_info(commit['full_hash'])
                    if pr_num:
                        content.append(f"- **{commit['hash']}** - {commit['subject']} ([#{pr_num}](link-to-pr))")
                    else:
                        content.append(f"- **{commit['hash']}** - {commit['subject']}")              
            else:
                content.append("- *No commit history found*")
    
    # Write to file
    with open(output_file, 'w') as f:
        f.write('\n'.join(content))
    
    print(f"\nMarkdown changelog saved to {output_file}")

if __name__ == "__main__":
    tag = "v1.14.2.4"
    
    # Option 1: Print to console
    generate_changelog(tag)
    
    # Option 2: Generate markdown file
    generate_markdown_changelog(tag)
