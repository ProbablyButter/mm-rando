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

def split_mod_file(data, prefix):
    pos = 0
    i = 1
    while pos < len(data):
        if (data[pos] == 0xff):
            return
        addr = struct.unpack_from('>I', data, pos)[0]
        pos += 4
        size = struct.unpack_from('>I', data, pos)[0]
        pos += 4
        payload = [hex(v) for v in data[pos:pos+size]]
        pos += size
        with open('mods/%s-%d'%(prefix,i), 'wb') as f:
            f.write(struct.pack('>I', addr))
            f.write(struct.pack('>I', size))
            f.write(struct.pack('%dB'%(size+1), *payload, 0xff))

with open('mods/misc-changes', mode='rb') as f:
    data = f.read()

split_mod_file(data, 'misc-changes')
