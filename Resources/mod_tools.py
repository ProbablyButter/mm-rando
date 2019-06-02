import struct

def bin_to_text(data, out):
    pos = 0
    while pos < len(data):
        if (data[pos] == 0xff):
            return
        addr = struct.unpack_from('>I', data, pos)[0]
        pos += 4
        size = struct.unpack_from('>I', data, pos)[0]
        pos += 4
        payload = [hex(v) for v in data[pos:pos+size]]
        pos += size
        print('%s, %s: %s'%(hex(addr), hex(size), payload), file=out)

def text_to_bin(data):
    pass

with open('mods/update-chests', mode='rb') as f:
    data = f.read()

with open('mods/update-chests.txt', mode='w') as f:
    bin_to_text(data, f)
