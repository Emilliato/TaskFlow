"""Builds the on-screen code panels FROM THE REAL SOURCE FILES (so what viewers see is what compiled).
Reads the files at git ref HEAD of the working tree, trims indentation, highlights C#, wraps zoom anchors.
Usage: python panels.py  -> writes panels.json (key -> html string, one logical line per newline)."""
import json, re, html, subprocess, sys

KW = set("""public private static async await void var new return if else foreach in using namespace class record interface
try catch throw is null true false this readonly override string int bool const out ref default from where select""".split())
TYPES = set("""Task IWebhookGateway SlowWebhookGateway CancellationToken ITaskService TaskItem TaskFlowDbContext IResult Results
ThreadPool Environment SynchronizationContext Thread ILogger ILoggerFactory InvalidOperationException OperationCanceledException
WebApplication DbContext Stopwatch List""".split())
PAT = re.compile(r'(//.*$)|(\$?"(?:[^"\\]|\\.)*")|(\b\d[\d_.]*\b)|([A-Za-z_][A-Za-z0-9_]*)|(.)', re.S)


def esc(s):
    return html.escape(s, quote=False)


def hl_segment(s):
    out = []
    for m in PAT.finditer(s):
        cm, st, num, ident, other = m.groups()
        if cm:
            out.append(f'<span class="cmt">{esc(cm)}</span>')
        elif st:
            out.append(f'<span class="str">{esc(st)}</span>')
        elif num:
            out.append(f'<span class="num">{esc(num)}</span>')
        elif ident:
            nxt = s[m.end():m.end() + 1]
            if ident in KW:
                out.append(f'<span class="kw">{ident}</span>')
            elif ident in TYPES or re.fullmatch(r'I[A-Z][A-Za-z]+', ident):
                out.append(f'<span class="type">{ident}</span>')
            elif nxt == '(':
                out.append(f'<span class="fn">{ident}</span>')
            else:
                out.append(ident)
        else:
            out.append(esc(other))
    return ''.join(out)


def hl_line(line, anchors):
    marks = []
    for aid, sub in anchors.items():
        i = line.find(sub)
        if i >= 0:
            marks.append((i, i + len(sub), aid))
    marks.sort()
    res, last = [], 0
    for a, b, aid in marks:
        if a < last:
            continue
        res.append(hl_segment(line[last:a]))
        res.append(f'<span id="z-{aid}">{hl_segment(line[a:b])}</span>')
        last = b
    res.append(hl_segment(line[last:]))
    return ''.join(res)


def read(path):
    return open(path, encoding='utf-8').read().replace('\r\n', '\n').split('\n')


def find(path, needle, start=0):
    for i, l in enumerate(read(path)[start:], start + 1):
        if needle in l:
            return i
    raise SystemExit(f'not found: {needle!r} in {path}')


def code(path, ranges, anchors=None, header=None, callouts=None, skip=()):
    """ranges: [(from,to)] 1-based inclusive. callouts: {line-substring: (id, text, cls)}. skip: substrings of lines to drop."""
    src = read(path)
    lines = []
    if header:
        lines.append(f'<div class="filename">{esc(header)}</div>')
    for k, (a, b) in enumerate(ranges):
        if k:
            lines.append('   <span class="dim">...</span>')
        chunk = src[a - 1:b]
        ind = min((len(l) - len(l.lstrip()) for l in chunk if l.strip()), default=0)
        for l in chunk:
            if any(s in l for s in skip):
                continue
            l = l[ind:] if l.strip() else l
            h = hl_line(l, anchors or {})
            for sub, (cid, txt, cls) in (callouts or {}).items():
                if sub in l:
                    h += f'<span class="callout {cls}" id="{cid}">{esc(txt)}</span>'
            lines.append('   ' + h)
    return '\n'.join(lines)


def build():
    P = {}
    AD, WG, EF, IT, PG = 'AsyncDemo.cs', 'WebhookGateway.cs', 'EfTaskService.cs', 'ITaskService.cs', 'Program.cs'

    a = find(AD, 'THE MISTAKE')
    P['blocking'] = code(AD, [(a + 1, a + 5)], {'result': '.Result'},
                         header='AsyncDemo.cs   -   the endpoint that passes every test',
                         callouts={'.Result': ('cBlock', 'parks this thread for 300 ms', 'warn')})
    a = find(AD, 'THE FIX')
    P['awaiting'] = code(AD, [(a + 1, a + 5)], {'await': 'await', 'ct': 'CancellationToken ct'},
                         header='AsyncDemo.cs   -   the same call, awaited',
                         callouts={'await gateway': ('cAwait', 'thread goes back to the pool', 'cap')})
    a = find(WG, 'public interface IWebhookGateway')
    b = find(WG, 'await Task.Delay')
    P['gateway'] = code(WG, [(a, a + 3), (b - 2, b + 1)],
                        {'taskret': 'Task<string>', 'delay': 'await Task.Delay(300, cancellationToken)'},
                        header='WebhookGateway.cs   -   work that finishes LATER hands back a Task',
                        skip=('// Stands in', '// not a thread'))
    a = find(AD, 'MISTAKE 2')
    P['asyncvoid'] = code(AD, [(a + 4, a + 8)], {'void': 'NotifyAndForget'},
                          header='AsyncDemo.cs   -   mistake 2')
    P['asyncvoid'] += '\n\n' + code(AD, [(find(AD, 'static async void'), find(AD, 'static async void') + 1)],
                                    {'avoid': 'async void'},
                                    callouts={'static async void': ('cVoid', 'nobody can await this', 'warn')})
    a = find(AD, 'MISTAKE 3')
    P['forgot'] = code(AD, [(find(AD, 'app.MapGet("/demo/forgot-await'), find(AD, 'app.MapGet("/demo/forgot-await') + 4)],
                       {'nowait': 'gateway.SendAsync($"task-{id}");'},
                       header='AsyncDemo.cs   -   mistake 3',
                       callouts={'gateway.SendAsync($"task-{id}");': ('cNoAwait', 'CS4014', 'warn')})
    a = find(IT, 'IQueryable<TaskItem> GetAll();')
    P['interface'] = code(IT, [(a, a + 4)], {'getbyid': 'Task<TaskItem?> GetByIdAsync'},
                          header='ITaskService.cs   -   after: everything that touches the database returns a Task')
    a = find(EF, 'public Task<TaskItem?> GetByIdAsync')
    b = find(EF, 'public async Task<TaskItem> AddAsync')
    P['efservice'] = code(EF, [(a, a + 1), (b, b + 10)],
                          {'fofd': 'FirstOrDefaultAsync', 'save': 'await _db.SaveChangesAsync(ct)'},
                          header='EfTaskService.cs   -   async all the way down to the database call',
                          skip=('// No Id is set', '// the INSERT', '// entity as part', '// state = Added'))
    a = find(PG, 'app.MapGet("/tasks", async (')
    b = find(PG, 'var results = await query')
    P['endpoint'] = code(PG, [(a, a + 8), (b - 3, b + 3)], {'tolist': '.ToListAsync(ct)', 'asyncl': 'async ('},
                         header='Program.cs   -   the endpoint awaits the query it used to hand to the JSON writer',
                         skip=('// MODEL BINDING', '// to the declared'))
    return P


if __name__ == '__main__':
    P = build()
    json.dump(P, open('panels.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    for k, v in P.items():
        print('==', k)
        print(re.sub(r'<[^>]+>', '', html.unescape(v)))
